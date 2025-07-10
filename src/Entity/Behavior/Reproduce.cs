using DetailedAnimals.Extensions;
using Genelib;
using Genelib.Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace DetailedAnimals {
    public class Reproduce : GeneticMultiply {
        public new const string Code = "genelib.reproduce";

        protected double LactationDays = 0;
        protected double EstrousCycleDays;
        protected double DaysInHeat;
        protected double litterAddChance = 0;
        protected int litterAddAttempts = 0;

        protected bool InducedOvulation = false;
        protected bool SeasonalBreeding = false;
        protected double BreedingSeasonPeak;
        protected double BreedingSeasonBefore;
        protected double BreedingSeasonAfter;

        public float MateTaskPriority = 1.5f;

        public virtual double ExtraGrowthTarget {
            get => 0;
        }

        public virtual double ExtraGrowthTargetHour {
            get => 0;
        }

        public double GrowthPausedSince {
            get => entity.WatchedAttributes.GetTreeAttribute("grow")?.GetDouble("growthPausedSince", -1) ?? entity.World.Calendar.TotalHours;
        }

        // Calendar.TotalDays includes timelapse adjustment, Calendar.TotalHours does not
        public virtual double TotalDays {
            get => entity.World.Calendar.TotalHours / 24.0;
        }

        public int NextGeneration() {
            int generation = entity.WatchedAttributes.GetInt("generation", 0);
            if (entity.WatchedAttributes.GetBool("fedByPlayer", false)) {
                return generation + 1;
            }
            return generation;
        }

        public Reproduce(Entity entity) : base(entity) { }

        // Pop as in stack push/pop
        public TreeAttribute PopChild() {
            TreeArrayAttribute litter = Litter;
            if (litter == null) {
                return null;
            }
            TreeAttribute child = litter.value[0];
            if (litter.value.Length <= 1) {
                SetNotPregnant();
            }
            else {
                litter.value = litter.value[1..^0];
                entity.WatchedAttributes.MarkPathDirty("multiply");
            }
            return child;
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes) {
            base.Initialize(properties, attributes);

            PregnancyDays *= AnimalConfig.AnimalGrowthTime;

            if (attributes.KeyExists("lactationMonths")) {
                LactationDays = attributes["lactationMonths"].AsDouble() * entity.World.Calendar.DaysPerMonth;
            }
            else if (attributes.KeyExists("lactationDays")) {
                LactationDays = attributes["lactationDays"].AsDouble();
            }
            LactationDays *= AnimalConfig.AnimalGrowthTime;
            MultiplyCooldownDaysMin *= AnimalConfig.AnimalGrowthTime;
            MultiplyCooldownDaysMax *= AnimalConfig.AnimalGrowthTime;

            InducedOvulation = attributes["inducedOvulation"].AsBool(false);
            if (InducedOvulation) {
                EstrousCycleDays = MultiplyCooldownDaysMax;
                DaysInHeat = EstrousCycleDays;
            }
            else {
                if (attributes.KeyExists("estrousCycleMonths")) {
                    EstrousCycleDays = attributes["estrousCycleMonths"].AsDouble() * entity.World.Calendar.DaysPerMonth;
                }
                else if (attributes.KeyExists("estrousCycleDays")) {
                    EstrousCycleDays = attributes["estrousCycleDays"].AsDouble();
                }
                else {
                    EstrousCycleDays = entity.World.Calendar.DaysPerMonth;
                }

                if (attributes.KeyExists("daysInHeat")) {
                    DaysInHeat = attributes["daysInHeat"].AsDouble();
                    DaysInHeat *= Math.Clamp(entity.World.Calendar.DaysPerMonth, 3, 9) / 9;
                }
                else {
                    DaysInHeat = 2;
                }
            }

            if (attributes.KeyExists("breedingPeakMonth")) {
                SeasonalBreeding = true;
                BreedingSeasonPeak = attributes["breedingPeakMonth"].AsDouble() / 12;
                BreedingSeasonBefore = attributes["breedingMonthsBefore"].AsDouble() / 12;
                BreedingSeasonAfter = attributes["breedingMonthsAfter"].AsDouble() / 12;
            }

            if (attributes.KeyExists("litterAddChance")) {
                litterAddChance = attributes["litterAddChance"].AsDouble();
            }
            if (attributes.KeyExists("litterAddAttempts")) {
                litterAddAttempts = attributes["litterAddAttempts"].AsInt();
            }

            if (attributes.KeyExists("mateTaskPriority")) {
                MateTaskPriority = attributes["mateTaskPriority"].AsFloat();
            }

            if (!entity.World.Side.IsServer()) {
                // Do not add multiply tree client-side or it won't sync
                return;
            }
            multiplyTree = entity.WatchedAttributes.GetOrAddTreeAttribute("multiply");

        }

        public override bool ShouldEat { get => true; }

        protected override bool TryGetPregnant() {
            if (entity.World.Rand.NextSingle() < 0.8f) {
                return false;
            }

            if (!EntityCanMate(this.entity)) {
                return false;
            }
            if (IsPregnant) {
                return false;
            }

            if (TotalDaysCooldownUntil + DaysInHeat < TotalDays) {
                TotalDaysCooldownUntil += EstrousCycleDays;
            }
            if (TotalDaysCooldownUntil > TotalDays) {
                return false;
            }

            if (!IsBreedingSeason()) {
                TotalDaysCooldownUntil += entity.World.Calendar.DaysPerMonth;
                return false;
            }

            EntityBehaviorTaskAI taskAi = entity.GetBehavior<EntityBehaviorTaskAI>();
            if (taskAi == null) {
                entity.Api.Logger.Warning(Code + ": Entity with code " + entity.Code + " has no task ai behavior and will be unable to breed");
                return false;
            }
            if (taskAi.TaskManager.ActiveTasksBySlot[0] is AiTaskMate) {
                // Already trying
                return false;
            }

            Entity sire = GetRequiredEntityNearby();
            if (sire == null) {
                return false;
            }

            AiTaskMate mateTask = new AiTaskMate((EntityAgent)entity, sire);
            mateTask.SetPriority(MateTaskPriority);
            taskAi.TaskManager.ExecuteTask(mateTask, 0);

            EntityBehaviorTaskAI sireTaskAi = sire.GetBehavior<EntityBehaviorTaskAI>();
            if (sireTaskAi == null) {
                entity.Api.Logger.Warning(Code + ": Potential sire entity with code " + sire.Code + " has no task ai behavior, this may cause difficulty for breeding");
                return false;
            }
            if (!(sireTaskAi.TaskManager.ActiveTasksBySlot[0] is AiTaskMate)) {
                AiTaskMate sireMateTask = new AiTaskMate((EntityAgent)sire, entity);
                sireMateTask.SetPriority(MateTaskPriority);
                sireTaskAi.TaskManager.ExecuteTask(sireMateTask, 0);
            }
            return false;
        }

        public override int ChooseLitterSize() {
            int litterSize = 1;
            for (int i = 0; i < litterAddAttempts; ++i) {
                if (entity.World.Rand.NextDouble() < litterAddChance) {
                    litterSize += 1;
                }
            }
            return litterSize;
        }

        public bool IsBreedingSeason(double season) {
            if (!SeasonalBreeding || BreedingSeasonBefore + BreedingSeasonAfter >= 1) {
                return true;
            }
            if (season > BreedingSeasonPeak) {
                season -= 1;
            }
            double timeUntilPeak = BreedingSeasonPeak - season;
            return timeUntilPeak < BreedingSeasonBefore || 1 - timeUntilPeak < BreedingSeasonAfter;
        }

        public bool IsBreedingSeason() {
            return IsBreedingSeason(entity.World.Calendar.GetSeasonRel(entity.Pos.AsBlockPos));
        }

        // If the animal dies, you lose the pregnancy even if you later revive it
        public override void OnEntityDeath(DamageSource damageSource) {
            SetNotPregnant();
        }

        // And on revival, if not pregnant, you do not progress towards becoming so
        public override void OnEntityRevive() {
            TotalDaysCooldownUntil += (entity.World.Calendar.TotalHours - GrowthPausedSince) / 24.0;
        }

        public virtual bool EntityHasEatenEnoughToMate(Entity entity) {
            double animalWeight = entity.BodyCondition();
            if (animalWeight <= DetailedHarvestable.MALNOURISHED || animalWeight > DetailedHarvestable.FAT) {
                return false;
            }
            return true;
        }

        protected override void GetReadinessInfoText(StringBuilder infotext, double animalWeight) {
            if (!entity.MatingAllowed()) {
                return;
            }
            if (animalWeight <= DetailedHarvestable.MALNOURISHED) {
                infotext.AppendLine(Lang.Get("detailedanimals:infotext-reproduce-underweight"));
                return;
            }
            else if (animalWeight > DetailedHarvestable.FAT) {
                infotext.AppendLine(Lang.Get("detailedanimals:infotext-reproduce-overweight"));
                return;
            }

            double daysLeft = TotalDaysCooldownUntil - TotalDays;
            IGameCalendar calendar = entity.World.Calendar;
            double season = (calendar.GetSeasonRel(entity.Pos.AsBlockPos) + daysLeft / calendar.DaysPerMonth / 12) % 1;
            if (!IsBreedingSeason(season)) {
                double breedingStart = (BreedingSeasonPeak - BreedingSeasonBefore + 1) % 1;
                if (breedingStart < 0.5) {
                    infotext.AppendLine(Lang.Get("detailedanimals:infotext-reproduce-longday"));
                }
                else {
                    infotext.AppendLine(Lang.Get("detailedanimals:infotext-reproduce-shortday"));
                }
                return;
            }

            if (daysLeft <= 0) {
                infotext.AppendLine(Lang.Get("game:Ready to mate"));
            }
            else if (daysLeft <= 4) {
                infotext.AppendLine(Lang.Get("genelib:infotext-multiply-waitdays" + Math.Ceiling(daysLeft).ToString()));
            }
            else {
                infotext.AppendLine(Lang.Get("game:Several days left before ready to mate"));
            }
        }

        public override string PropertyName() => Code;
    }
}
