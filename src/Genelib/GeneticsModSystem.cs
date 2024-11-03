using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;

﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.Common;
using Vintagestory.GameContent;
using Vintagestory.API.Util;

using Genelib.Extensions;

namespace Genelib
{
    public class GeneticsModSystem : ModSystem
    {
        public static GenelibConfig Config = null;
        public static AssetCategory genetics = null;
        public static AssetCategory nutrition = null;
        public const string NamePrefix = "genelib.";

        public static double MutationRate = 0.00004;
        public static double AnimalGrowthTime = 1.0;

        internal static ICoreServerAPI ServerAPI { get; private set; }
        internal static ICoreClientAPI ClientAPI { get; private set; }

        // Called during intial mod loading, called before any mod receives the call to Start()
        public override void StartPre(ICoreAPI api) {
            genetics = new AssetCategory(nameof(genetics), true, EnumAppSide.Server);
            nutrition = new AssetCategory(nameof(nutrition), true, EnumAppSide.Server);
        }

        // Called on server and client
        public override void Start(ICoreAPI api) {
            HarmonyPatches.Patch();

            api.RegisterBlockClass("Genelib.BlockNestbox", typeof(BlockGeneticNestbox));
            api.RegisterBlockEntityClass("Genelib.Nestbox", typeof(GeneticNestbox));

            api.RegisterEntityBehaviorClass(EntityBehaviorGenetics.Code, typeof(EntityBehaviorGenetics));
            api.RegisterEntityBehaviorClass(Reproduce.Code, typeof(Reproduce));
            api.RegisterEntityBehaviorClass(BehaviorAge.Code, typeof(BehaviorAge));
            api.RegisterEntityBehaviorClass(DetailedHarvestable.Code, typeof(DetailedHarvestable));
            api.RegisterEntityBehaviorClass(AnimalHunger.Code, typeof(AnimalHunger));

            api.RegisterCollectibleBehaviorClass(TryFeedingAnimal.Code, typeof(TryFeedingAnimal));

            AiTaskRegistry.Register("genelib.forage", typeof (AiTaskForage));
            AiTaskRegistry.Register("genelib.layegg", typeof (AiTaskLayEgg));

            GenomeType.RegisterInterpreter("Polygenes", new PolygeneInterpreter());

            try {
                Config = api.LoadModConfig<GenelibConfig>("genelib_config.json");
            }
            catch (Exception e) {
                api.Logger.Error("Failed to load config file for Genelib: " + e);
            }
            if (Config == null) {
                Config = new GenelibConfig();
            }
            api.StoreModConfig(Config, "genelib_config.json");
        }

        public override void AssetsLoaded(ICoreAPI api) {
            LoadAssetType(api, genetics.Code, (asset) => GenomeType.Load(asset), "genome types");
            LoadAssetType(api, nutrition.Code, (asset) => NutritionData.Load(asset), "nutrition datasets");
        }

        public void LoadAssetType(ICoreAPI api, string category, Action<IAsset> onLoaded, string typeName) {
            List<IAsset> assets = api.Assets.GetManyInCategory(category, "");
            foreach (IAsset asset in assets) {
                try {
                    onLoaded(asset);
                }
                catch (Exception e) {
                    api.Logger.Error("Error loading asset " + asset.Location.ToString() + ". " + e.Message + "\n" + e.StackTrace);
                }
            }
            api.Logger.Event(assets.Count + " " + typeName + " loaded");
        }

        public override void AssetsFinalize(ICoreAPI api) {
            if (api.Side != EnumAppSide.Server) {
                return;
            }

            foreach (EntityProperties entityType in api.World.EntityTypes) {
                foreach (JsonObject jsonObject in entityType.Server.BehaviorsAsJsonObj) {
                    // Need to do the same thing as ModSystemSyncHarvestableDropsToClient
                    // so detailedharvestable drops show up in the handbook client-side
                    if (jsonObject["code"].AsString() == DetailedHarvestable.Code) {
                        if (entityType.Attributes == null) {
                            entityType.Attributes = new JsonObject(JToken.Parse("{}"));
                        }
                        entityType.Attributes.Token["harvestableDrops"] = jsonObject["drops"].Token;
                    }
                    // Sync over reproduce setup so infotext displayed will be correct
                    else if (jsonObject["code"].AsString() == Reproduce.Code) {
                        for (int i = 0; i < entityType.Client.BehaviorsAsJsonObj.Length; ++i) {
                            JsonObject clientJson = entityType.Client.BehaviorsAsJsonObj[i];
                            if (clientJson["code"].AsString() == Reproduce.Code) {
                                entityType.Client.BehaviorsAsJsonObj[i] = jsonObject;
                            }
                        }
                    }
                    // Also pre-process some aging stuff
                    else if (jsonObject["code"].AsString() == BehaviorAge.Code) {
                        if (entityType.Attributes == null) {
                            entityType.Attributes = new JsonObject(JToken.Parse("{}"));
                        }
                        if (jsonObject.KeyExists("initialWeight")) {
                            entityType.Attributes.Token["initialWeight"] = jsonObject["initialWeight"].Token;
                        }
                    }
                }
                // Also sanity check weight dimorphism
                float weightDimorphism = entityType.Attributes?["weightDimorphism"].AsFloat(0) ?? 0;
                if (weightDimorphism >= 1 || weightDimorphism <= -1) {
                    api.Logger.Warning("Attribute weightDimorphism for entity type " + entityType.Code 
                        + " is outside the range (-1, 1). This may result in entities with negative weight.");
                }
            }
            // If it has nutritional value, you can try feeding it to an animal
            foreach (CollectibleObject item in api.World.Collectibles) {
                if (item.Code == null) {
                    continue;
                }
                if (item.NutritionProps != null || item.Attributes?["foodTags"].Exists == true) {
                    if (item.GetBehavior(typeof(TryFeedingAnimal)) != null) {
                        continue;
                    }
                    item.CollectibleBehaviors = item.CollectibleBehaviors.InsertAt<CollectibleBehavior>(new TryFeedingAnimal(item), 0);
                }
            }
        }

        public override void StartServerSide(ICoreServerAPI api) {
            ServerAPI = api;
        }

        public override void StartClientSide(ICoreClientAPI api) {
            ClientAPI = api;
        }
    }
}
