using Genelib.Extensions;
using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Genelib {
    public class ReproduceEgg : Reproduce {
        public const string Code = "genelib.eggreproduce";
        private const float DEFAULT_WEIGHT = 0.04f;

        public CollectibleObject[] EggTypes;
        protected AiTaskLayEgg layEggTask;
        protected EntityBehaviorTaskAI taskAI;
        // Used temporarily for holding data after Initialize() until AfterInitialized()
        private Type taskType;
        private JsonObject taskConfig;

        public ReproduceEgg(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject attributes) {
            base.Initialize(properties, attributes);

            JsonItemStack[] eggs = entity.Properties.Attributes?["eggTypes"].AsArray<JsonItemStack>();
            if (eggs != null) {
                EggTypes = eggs.Select(
                    (jsonEgg) => {
                        if (jsonEgg.Resolve(entity.World, null, false)) {
                            return jsonEgg.ResolvedItemstack.Collectible;
                        }
                        else {
                            entity.Api.Logger.Warning("Failed to resolve egg " + jsonEgg.Type + " with code " + jsonEgg.Code + " for entity " + entity.Code);
                            return null;
                        }
                    } 
                ).Where(x => x != null).ToArray();

                Array.Sort(EggTypes, (x, y) => 
                    (x.Attributes?["weightKg"].AsFloat(DEFAULT_WEIGHT) ?? DEFAULT_WEIGHT)
                    .CompareTo(y.Attributes?["weightKg"].AsFloat(DEFAULT_WEIGHT) ?? DEFAULT_WEIGHT)
                );
            }

            if (entity.Api.Side != EnumAppSide.Server) {
                return;
            }

            taskConfig = attributes["layeggtask"];
            string code = taskConfig["code"]?.AsString();
            if (code == null) {
                throw new FormatException(Code + " has layeggtask with null code for entity " + entity.Code);
            }
            if (!AiTaskRegistry.TaskTypes.TryGetValue(code, out taskType)) {
                throw new FormatException(Code + " no class registered for layeggtask with code " + code + " for entity " + entity.Code);
            }
        }

        public override void AfterInitialized(bool onFirstSpawn) {
            base.AfterInitialized(onFirstSpawn);
            if (entity.Api.Side != EnumAppSide.Server) {
                return;
            }

            taskAI = entity.GetBehavior<EntityBehaviorTaskAI>();
            if (taskAI == null) {
                throw new FormatException("No taskai behavior found for " + entity.Code + " needed by " + Code);
            }

            EntityAgent entityAgent = entity as EntityAgent;
            if (entityAgent == null) {
                throw new FormatException(Code + " requires an EntityAgent");
            }
            try {
                layEggTask = (AiTaskLayEgg)Activator.CreateInstance(taskType, entityAgent);
            }
            catch (Exception e) {
                if (e.InnerException != null) {
                    entity.Api.Logger.Error("Exception loading " + Code + " for " + entity.Code);
                    entity.Api.Logger.Error(e.InnerException);
                }
                throw e;
            }
            layEggTask.LoadConfig(taskConfig, null);
            taskConfig = null;
            layEggTask.AfterInitialize();
            taskAI.TaskManager.AddTask(layEggTask);
        }

        protected override void ProgressPregnancy() {
            if (TotalDays > TotalDaysPregnancyStart + GestationDays) {
                SetNotPregnant();
            }
        }

        public bool CanLayEgg() {
            if (!entity.Alive || entity.WatchedAttributes.GetBool("neutered", false)) {
                return false;
            }
            double animalWeight = entity.BodyCondition();
            if (animalWeight <= DetailedHarvestable.UNDERWEIGHT) {
                return false;
            }
            return true;
        }

        // The resulting itemstack does not come with incubation data
        public ItemStack GiveEgg() {
            float eggWeight = 0.051f; // TODO: Make different chickens lay different sizes of egg

            CollectibleObject egg = EggTypes[0];
            float lessw;
            for (int i = 1; i < EggTypes.Length; ++i) {
                float w = EggTypes[i].Attributes?["weightKg"].AsFloat(DEFAULT_WEIGHT) ?? DEFAULT_WEIGHT;
                if (w == eggWeight) {
                    egg = EggTypes[i];
                    break;
                }
                else if (w > eggWeight) {
                    lessw = EggTypes[i-1].Attributes?["weightKg"].AsFloat(DEFAULT_WEIGHT) ?? DEFAULT_WEIGHT;
                    float r = lessw + entity.World.Rand.NextSingle() * (w - lessw);
                    egg = EggTypes[r > eggWeight ? i : i - 1];
                    eggWeight = w;
                    break;
                }
                lessw = w;
            }

            float theRestOfTheWeight = entity.Properties.Attributes["adultWeightKg"].AsFloat() * entity.WeightModifierExceptCondition();
            double prevTotalWeight = entity.BodyCondition() * theRestOfTheWeight;
            double newTotalWeight = Math.Max(prevTotalWeight * 0.1f, prevTotalWeight - eggWeight);
            entity.SetBodyCondition(newTotalWeight / theRestOfTheWeight);

            ItemStack eggStack = new ItemStack(egg);
            TreeAttribute chick = PopChild();
            if (chick == null) {
                return eggStack;
            }

            chick.SetInt("generation", NextGeneration());
            eggStack.Attributes["chick"] = chick;

            return eggStack;
        }

        public override void GetInfoText(StringBuilder infotext) {
            if (!entity.Alive) {
                return;
            }
            multiplyTree = entity.WatchedAttributes.GetTreeAttribute("multiply");
            if (entity.WatchedAttributes.GetBool("neutered", false)) {
                return;
            }
            double animalWeight = entity.BodyCondition();
            if (animalWeight <= DetailedHarvestable.UNDERWEIGHT) {
                infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-underweight-eggs"));
                return;
            }
            if (IsPregnant) {
                string key = "genelib:infotext-reproduce-eggsfertile";
                string translated = Lang.AvailableLanguages[Lang.CurrentLocale].GetUnformatted(key);
                infotext.AppendLine((key != translated) ? translated : Lang.Get("game:Ready to lay"));
                return;
            }
            GetRemainingInfoText(infotext, animalWeight);
        }

    }
}
