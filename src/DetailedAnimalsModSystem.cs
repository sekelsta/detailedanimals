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

using DetailedAnimals.Network;
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

        public override void Start(ICoreAPI api)
        {
            HarmonyPatches.Patch();

            api.RegisterEntityBehaviorClass(Reproduce.Code, typeof(Reproduce));
            api.RegisterEntityBehaviorClass(ReproduceEgg.Code, typeof(ReproduceEgg));
            api.RegisterEntityBehaviorClass(BehaviorAge.Code, typeof(BehaviorAge));
            api.RegisterEntityBehaviorClass(DetailedHarvestable.Code, typeof(DetailedHarvestable));
            api.RegisterEntityBehaviorClass(AnimalHunger.Code, typeof(AnimalHunger));
            api.RegisterEntityBehaviorClass(BehaviorAnimalInfo.Code, typeof(BehaviorAnimalInfo));

            api.RegisterCollectibleBehaviorClass(TryFeedingAnimal.Code, typeof(TryFeedingAnimal));

            AiTaskRegistry.Register("genelib.forage", typeof (AiTaskForage));
            AiTaskRegistry.Register("genelib.sitonnest", typeof (AiTaskSitOnNest));
            AiTaskRegistry.Register("genelib.layegg", typeof (AiTaskLayEgg));

            AnimalConfig.Load(api);

            GenomeType.RegisterInterpreter(new JunglefowlGenetics());
            GenomeType.RegisterInterpreter(new GoatGenetics());
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

        public override void StartServerSide(ICoreServerAPI api) {
            ServerAPI = api;
            api.Network.RegisterChannel("detailedanimals")
                .RegisterMessageType<SetNameMessage>().SetMessageHandler<SetNameMessage>(OnSetNameMessageServer)
                .RegisterMessageType<SetNoteMessage>().SetMessageHandler<SetNoteMessage>(OnSetNoteMessageServer)
                .RegisterMessageType<ToggleBreedingMessage>().SetMessageHandler<ToggleBreedingMessage>(OnToggleBreedingMessageServer);
        }

        public override void StartClientSide(ICoreClientAPI api) {
            ClientAPI = api;
            api.Network.RegisterChannel("detailedanimals")
                .RegisterMessageType<SetNameMessage>()
                .RegisterMessageType<SetNoteMessage>()
                .RegisterMessageType<ToggleBreedingMessage>();

            api.Input.RegisterHotKey("detailedanimals.info", Lang.Get("detailedanimals:gui-hotkey-animalinfo"), GlKeys.N, type: HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("detailedanimals.info", ToggleAnimalInfoGUI);
        }

        public bool ToggleAnimalInfoGUI(KeyCombination keyConbination) {
            foreach (GuiDialog dialog in ClientAPI.Gui.OpenedGuis) {
                if (dialog is GuiDialogAnimal && dialog.IsOpened()) {
                    dialog.TryClose();
                    return true;
                }
            }

            EntityPlayer player = (ClientAPI.World as ClientMain)?.EntityPlayer;
            EntitySelection entitySelection = player?.EntitySelection;
            EntityAgent agent = entitySelection?.Entity as EntityAgent;
            if (agent == null 
                    || !agent.Alive 
                    || agent.GetBehavior<BehaviorAnimalInfo>() == null 
                    || agent.Pos.SquareDistanceTo(player.Pos.XYZ) > 20 * 20) {
                return false;
            }
            GuiDialogAnimal animalDialog = new GuiDialogAnimal(ClientAPI, agent);
            animalDialog.TryOpen();
            return true;
        }

        private void OnSetNameMessageServer(IServerPlayer fromPlayer, SetNameMessage message) {
            Entity target = ServerAPI.World.GetEntityById(message.entityId);
            EntityBehaviorNameTag nametag = target.GetBehavior<EntityBehaviorNameTag>();
            if (nametag == null || target.OwnedByOther(fromPlayer)) {
                return;
            }
            target.Api.Logger.Audit(fromPlayer.PlayerName + " changed name of " + target.Code + " ID " + target.EntityId + " at " + target.Pos.XYZ.AsBlockPos 
                + " from " + nametag.DisplayName + " to " + message.name);
            nametag.SetName(message.name);
        }

        private void OnSetNoteMessageServer(IServerPlayer fromPlayer, SetNoteMessage message) {
            Entity target = ServerAPI.World.GetEntityById(message.entityId);
            BehaviorAnimalInfo info = target.GetBehavior<BehaviorAnimalInfo>();
            if (info == null || target.OwnedByOther(fromPlayer)) {
                return;
            }
            target.Api.Logger.Audit(fromPlayer.PlayerName + " changed note of " + target.Code + " ID " + target.EntityId + " at " + target.Pos.XYZ.AsBlockPos 
                + " from " + info.Note + " to " + message.note);
            info.Note = message.note;
        }

        private void OnToggleBreedingMessageServer(IServerPlayer fromPlayer, ToggleBreedingMessage message) {
            Entity target = ServerAPI.World.GetEntityById(message.entityId);
            if (target.OwnedByOther(fromPlayer)) {
                return;
            }
            ITreeAttribute domestication = target.WatchedAttributes.GetTreeAttribute("domesticationstatus");
            if (domestication != null) {
                domestication.SetBool("multiplyAllowed", !message.preventBreeding);
            }
            else {
                target.WatchedAttributes.SetBool("preventBreeding", message.preventBreeding);
            }
            target.Api.Logger.Audit(fromPlayer.PlayerName + " set preventBreeding=" + message.preventBreeding + " for " + target.Code + " ID " + target.EntityId + " at " + target.Pos.XYZ.AsBlockPos);
        }
    }
}
