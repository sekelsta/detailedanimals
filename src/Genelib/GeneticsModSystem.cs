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
        public const string NamePrefix = "genelib.";

        public static double MutationRate = 0.00004;

        internal static ICoreServerAPI ServerAPI { get; private set; }
        internal static ICoreClientAPI ClientAPI { get; private set; }

        // Called during intial mod loading, called before any mod receives the call to Start()
        public override void StartPre(ICoreAPI api) {
            genetics = new AssetCategory(nameof(genetics), true, EnumAppSide.Server);
        }

        // Called on server and client
        public override void Start(ICoreAPI api) {
            api.RegisterEntityBehaviorClass(EntityBehaviorGenetics.Code, typeof(EntityBehaviorGenetics));
            api.RegisterEntityBehaviorClass(Reproduce.Code, typeof(Reproduce));
            api.RegisterEntityBehaviorClass(BehaviorAge.Code, typeof(BehaviorAge));
            api.RegisterEntityBehaviorClass(DetailedHarvestable.Code, typeof(DetailedHarvestable));
            api.RegisterEntityBehaviorClass(AnimalHunger.Code, typeof(AnimalHunger));

            api.RegisterCollectibleBehaviorClass(TryFeedingAnimal.Code, typeof(TryFeedingAnimal));

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
        }

        public override void AssetsLoaded(ICoreAPI api) {
            List<IAsset> assets = api.Assets.GetManyInCategory(genetics.Code, "");
            foreach (IAsset asset in assets) {
                try {
                    GenomeType.Load(asset);
                }
                catch (Exception e) {
                    api.Logger.Error("Error loading genome type " + asset.Location.ToString() + ". " + e.Message + "\n" + e.StackTrace);
                }
            }
            api.Logger.Event(assets.Count + " genome types loaded");
        }

        public override void AssetsFinalize(ICoreAPI api) {
            if (api.World.Side != EnumAppSide.Server) {
                return;
            }
            // Need to do the same thing as ModSystemSyncHarvestableDropsToClient
            // so detailedharvestable drops show up in the handbook client-side
            foreach (EntityProperties entityType in api.World.EntityTypes) {
                foreach (JsonObject jsonObject in entityType.Server.BehaviorsAsJsonObj) {
                    if (jsonObject["code"].AsString() == DetailedHarvestable.Code) {
                        if (entityType.Attributes == null) {
                            entityType.Attributes = new JsonObject(JToken.Parse("{}"));
                        }
                        entityType.Attributes.Token["harvestableDrops"] = jsonObject["drops"].Token;
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
                    api.Logger.Notification("sekelstadebug " + item.Code);
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
