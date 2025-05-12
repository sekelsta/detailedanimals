using DetailedAnimals.Extensions;
using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace DetailedAnimals {
    public class ReproduceEgg : Reproduce {
        new public const string Code = "genelib.eggreproduce";
        private const float DEFAULT_WEIGHT = 0.04f;

        public CollectibleObject[] EggTypes;
        public double IncubationDays;
        public bool IncubationScalesWithMonthLength = true;
        public NatFloat HoursPerEgg;

        protected AiTaskLayEgg layEggTask;
        protected bool layEggTaskActive = false;
        protected EntityBehaviorTaskAI taskAI;
        // Used temporarily for holding data after Initialize() until AfterInitialized()
        private Type taskType;
        private JsonObject taskConfig;

        private const float averageEggWeight = 0.051f; // TODO: Make different chickens lay different sizes of egg

        public double EggLaidHours {
            get => entity.WatchedAttributes.GetDouble("eggLaidHours");
            set => entity.WatchedAttributes.SetDouble("eggLaidHours", value);
        }

        public double NextEggHours {
            get => entity.WatchedAttributes.GetDouble("nextEggHours");
            set => entity.WatchedAttributes.SetDouble("nextEggHours", value);
        }

        public override double ExtraGrowthTarget {
            get {
                double next = NextEggHours;
                double total = entity.World.Calendar.TotalHours;
                return next > total && next < total + 26 ? averageEggWeight / entity.BaseWeight() : 0;
            }
        }

        public override double ExtraGrowthTargetHour {
            get => NextEggHours;
        }

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

            IncubationDays = attributes["incubationMonths"].AsDouble(1) * entity.World.Calendar.DaysPerMonth;
            HoursPerEgg = attributes["hoursPerEgg"].AsObject<NatFloat>();

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
                throw;
            }
            layEggTask.LoadConfig(taskConfig, null);
            taskConfig = null;
            layEggTask.AfterInitialize();
            if (CanLayEgg()) {
                taskAI.TaskManager.AddTask(layEggTask);
                layEggTaskActive = true;
            }
        }

        protected override void SlowTick(float dt) {
            if (!entity.World.Side.IsServer()) {
                return;
            }
            base.SlowTick(dt);
            if (CanLayEgg()) {
                if (!layEggTaskActive) {
                    taskAI.TaskManager.AddTask(layEggTask);
                    layEggTaskActive = true;
                }
            }
            else {
                if (layEggTaskActive) {
                    taskAI.TaskManager.RemoveTask(layEggTask);
                    layEggTaskActive = false;
                }
            }
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
            if (NextEggHours > entity.World.Calendar.TotalHours) {
                return false;
            }
            double animalWeight = entity.BodyCondition();
            double lastBroody = entity.WatchedAttributes.GetDouble("lastBroodyHours", -1);
            if (animalWeight <= DetailedHarvestable.UNDERWEIGHT 
                    || lastBroody > entity.World.Calendar.TotalHours - 72
                    || !IsBreedingSeason()
                    || TotalDaysCooldownUntil > TotalDays) {
                NextEggHours = entity.World.Calendar.TotalHours + HoursPerEgg.nextFloat(1, entity.World.Rand);
                return false;
            }
            return true;
        }

        public ItemStack LayEgg() {
            CollectibleObject egg = EggTypes[0];
            float eggWeight = averageEggWeight;
            float lessw;
            for (int i = 1; i < EggTypes.Length; ++i) {
                float w = EggTypes[i].Attributes?["weightKg"].AsFloat(DEFAULT_WEIGHT) ?? DEFAULT_WEIGHT;
                if (w == averageEggWeight) {
                    egg = EggTypes[i];
                    break;
                }
                else if (w > averageEggWeight) {
                    lessw = EggTypes[i-1].Attributes?["weightKg"].AsFloat(DEFAULT_WEIGHT) ?? DEFAULT_WEIGHT;
                    float r = lessw + entity.World.Rand.NextSingle() * (w - lessw);
                    egg = EggTypes[r > eggWeight ? i : i - 1];
                    eggWeight = w;
                    break;
                }
                lessw = w;
            }

            float adultWeightKg = entity.Properties.Attributes["adultWeightKg"].AsFloat();
            double leftover = entity.ExtraGrowth() - eggWeight / adultWeightKg;
            entity.SetExtraGrowth(Math.Max(0, leftover));
            if (leftover < 0) {
                float baseWeight = entity.BaseWeight();
                double prevWeight = entity.BodyCondition() * baseWeight;
                double newWeight = Math.Max(prevWeight * 0.1f, prevWeight + leftover);
                entity.SetBodyCondition(newWeight / baseWeight);
            }

            ItemStack eggStack = new ItemStack(egg);
            EggLaidHours = entity.World.Calendar.TotalHours;
            NextEggHours = entity.World.Calendar.TotalHours + HoursPerEgg.nextFloat(1, entity.World.Rand);

            TreeAttribute chick = PopChild();
            if (chick == null) {
                return eggStack;
            }
            chick.SetInt("generation", NextGeneration());
            eggStack.Attributes["chick"] = chick;

            double incubationHoursTotal = IncubationDays * 24 * AnimalConfig.AnimalGrowthTime;
            eggStack.Attributes.SetDouble("incubationHoursRemaining", incubationHoursTotal);
            eggStack.Attributes.SetDouble("incubationHoursTotal", incubationHoursTotal);
            // If incubation length scales with month length, freshness should too
            if (IncubationScalesWithMonthLength) {
                TransitionState[] transitions = eggStack.Collectible?.UpdateAndGetTransitionStates(entity.World, new DummySlot(eggStack));
                // Note calling UpdateAndGetTransitionStates may set the itemstack to null e.g. if it rotted with 50% conversion rate
                if (transitions != null && eggStack.Collectible != null) {
                    for (int i = 0; i < transitions.Length; ++i) {
                        if (transitions[i].Props.Type == EnumTransitionType.Perish) {
                            ITreeAttribute attr = (ITreeAttribute)eggStack.Attributes["transitionstate"];
                            float[] freshHours = (attr["freshHours"] as FloatArrayAttribute).value;
                            float adjusted = freshHours[i] * entity.World.Calendar.DaysPerMonth / 9f * GlobalConstants.PerishSpeedModifier;
                            freshHours[i] = (float)Math.Max(adjusted, 6 * 24 + incubationHoursTotal);
                        }
                    }
                }
            }

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
            if (animalWeight <= DetailedHarvestable.MALNOURISHED) {
                infotext.AppendLine(Lang.Get("detailedanimals:infotext-reproduce-underweight"));
                return;
            }
            double lastBroody = entity.WatchedAttributes.GetDouble("lastBroodyHours", -1);
            if (lastBroody > entity.World.Calendar.TotalHours - 72) {
                infotext.AppendLine(Lang.Get("detailedanimals:infotext-reproduce-broody"));
                return;
            }
            if (animalWeight <= DetailedHarvestable.UNDERWEIGHT) {
                infotext.AppendLine(Lang.Get("detailedanimals:infotext-reproduce-underweight-eggs"));
                return;
            }
            if (IsPregnant) {
                string key = "detailedanimals:infotext-reproduce-eggsfertile";
                string translated = Lang.AvailableLanguages[Lang.CurrentLocale].GetUnformatted(key);
                infotext.AppendLine((key != translated) ? translated : Lang.Get("game:Ready to lay"));
                return;
            }
            GetRemainingInfoText(infotext, animalWeight);
        }

    }
}
