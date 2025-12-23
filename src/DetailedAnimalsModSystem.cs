using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using Vintagestory.GameContent;

using Genelib;
using Genelib.Extensions;

namespace DetailedAnimals
{
    public class DetailedAnimalsModSystem : ModSystem
    {
        public static readonly string modid = "detailedanimals";
        public static AssetCategory nutrition = null;

        internal static ICoreServerAPI ServerAPI { get; private set; }
        internal static ICoreClientAPI ClientAPI { get; private set; }
        internal static ICoreAPI API => (ICoreAPI)ServerAPI ?? (ICoreAPI)ClientAPI;

        // Called during intial mod loading, called before any mod receives the call to Start()
        public override void StartPre(ICoreAPI api) {
            nutrition = new AssetCategory(nameof(nutrition), true, EnumAppSide.Server);
        }

        public override void Start(ICoreAPI api)
        {
            HarmonyPatches.Patch();

            api.RegisterEntityBehaviorClass(Reproduce.Code, typeof(Reproduce));
            api.RegisterEntityBehaviorClass(ReproduceEgg.Code, typeof(ReproduceEgg));
            api.RegisterEntityBehaviorClass(BehaviorAge.Code, typeof(BehaviorAge));
            api.RegisterEntityBehaviorClass(DetailedHarvestable.Code, typeof(DetailedHarvestable));
            api.RegisterEntityBehaviorClass(AnimalHunger.Code, typeof(AnimalHunger));

            api.RegisterCollectibleBehaviorClass(TryFeedingAnimal.Code, typeof(TryFeedingAnimal));

            AiTaskRegistry.Register<AiTaskForage>("forage");
            AiTaskRegistry.Register<AiTaskSitOnNest>("sitonnest");
            AiTaskRegistry.Register<AiTaskLayEgg>("layegg");

            AnimalConfig.Load(api);

            GenomeType.RegisterInterpreter(new JunglefowlGenetics());
            GenomeType.RegisterInterpreter(new GoatGenetics());
            GenomeType.RegisterInterpreter(new CanineGenetics());

            GenelibSystem.AutoadjustAnimalBehaviors = true;
        }

        public override void StartServerSide(ICoreServerAPI api) {
            ServerAPI = api;
        }

        public override void StartClientSide(ICoreClientAPI api) {
            ClientAPI = api;

	    GuiDialogAnimal.AddToStatusContents -= AddHunger;
            GuiDialogAnimal.AddToStatusContents += AddHunger;
	    GuiDialogAnimal.AddPreInfoContents -= AddAge;
            GuiDialogAnimal.AddPreInfoContents += AddAge;
        }

        private static void AddHunger(GuiDialogAnimal gui, ref int y) {
            AnimalHunger hunger = gui.Animal.GetBehavior<AnimalHunger>();
            if (hunger != null) {
                y += 5;
                gui.SingleComposer.AddStaticText(Lang.Get("playerinfo-nutrition"), CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold), ElementBounds.Fixed(0, y, gui.Width, 25));
                y += 25;
                foreach (Nutrient nutrient in hunger.Nutrients) {
                    if (nutrient.Name == "water" || nutrient.Name == "minerals") {
                        // Don't display these until the player has a way to feed them
                        continue;
                    }
                    string n = Lang.Get("detailedanimals:gui-animalinfo-amount-" + nutrient.Amount);
                    string f = Lang.Get("detailedanimals:gui-animalinfo-amount-f-" + nutrient.Amount);
                    string m = Lang.Get("detailedanimals:gui-animalinfo-amount-m-" + nutrient.Amount);
                    string text = Lang.GetUnformatted("detailedanimals:gui-animalinfo-nutrient-" + nutrient.Name)
                        .Replace("{n}", n).Replace("{m}", m).Replace("{f}", f);
                    gui.SingleComposer.AddStaticText(text, CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, gui.Width, 25));
                    y += 20;
                }
                y += 5;
            }
        }

        private static void AddAge(GuiDialogAnimal gui, ref int y) {
            BehaviorAge age = gui.Animal.GetBehavior<BehaviorAge>();
            if (age == null) {
                return;
            }
            if (gui.Animal.WatchedAttributes.HasAttribute("birthTotalDays")) {
                double birthDate = gui.Animal.WatchedAttributes.GetDouble("birthTotalDays");
                double ageDays = gui.Animal.World.Calendar.TotalDays - birthDate;
                string ageText = Lang.Get("detailedanimals:gui-animalinfo-age", VSExtensions.TranslateTimeFromHours(gui.Animal.Api, ageDays * 24 * GenelibConfig.AnimalYearSpeed));
                gui.SingleComposer.AddStaticText(ageText, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, gui.Width, 25));
                y += 25;
            }
        }

        public override void AssetsLoaded(ICoreAPI api) {
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
                JsonObject serverReproduce = null;
                foreach (JsonObject jsonObject in entityType.Server.BehaviorsAsJsonObj) {
                    string code = jsonObject["code"].AsString();
                    // Need to do the same thing as ModSystemSyncHarvestableDropsToClient
                    // so detailedharvestable drops show up in the handbook client-side
                    if (code == DetailedHarvestable.Code) {
                        if (entityType.Attributes == null) {
                            entityType.Attributes = new JsonObject(JToken.Parse("{}"));
                        }
                        entityType.Attributes.Token["harvestableDrops"] = jsonObject["drops"].Token;
                    }
                    else if (code == Reproduce.Code || code == ReproduceEgg.Code) {
                        serverReproduce = jsonObject;
                    }
                    // Also pre-process some aging stuff
                    else if (code == BehaviorAge.Code) {
                        if (entityType.Attributes == null) {
                            entityType.Attributes = new JsonObject(JToken.Parse("{}"));
                        }
                        if (jsonObject.KeyExists("initialWeight")) {
                            entityType.Attributes.Token["initialWeight"] = jsonObject["initialWeight"].Token;
                        }
                    }
                }
                // Sync over reproduce and hunger setups so info displayed will be correct
                for (int i = 0; i < entityType.Client.BehaviorsAsJsonObj.Length; ++i) {
                    JsonObject clientJson = entityType.Client.BehaviorsAsJsonObj[i];
                    string code = clientJson["code"].AsString();
                    if ((code == Reproduce.Code || code == ReproduceEgg.Code) && serverReproduce != null) {
                        entityType.Client.BehaviorsAsJsonObj[i] = serverReproduce;
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
    }
}
