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

        // TODO: rearrange to handle hybrids - e.g., offspringBySire
        protected AssetLocation[] SireCodes;
        protected float SireSearchRange;
        protected long listenerID;
        protected double CooldownDays;
        protected double GestationDays;
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

        public bool InEarlyPregnancy {
            get => multiplyTree.GetBool("earlyPregnancy", true);
            set {
                multiplyTree.SetBool("earlyPregnancy", value);
                entity.WatchedAttributes.MarkPathDirty("multiply");
            }
        }
        public double GrowthPausedSince {
            get => entity.WatchedAttributes.GetTreeAttribute("grow")?.GetDouble("growthPausedSince", -1) ?? entity.World.Calendar.TotalHours;
        }

        // Calendar.TotalDays includes timelapse adjustment, Calendar.TotalHours does not
        public virtual double TotalDays {
            get => entity.World.Calendar.TotalHours / 24.0;
        }

        protected double TotalDaysPregnancyEnd {
            get => multiplyTree.GetDouble("totalDaysPregnancyEnd");
            set {
                multiplyTree.SetDouble("totalDaysPregnancyEnd", value);
                entity.WatchedAttributes.MarkPathDirty("multiply");
            }
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
            // Deliberately skip calling base.Initialize()

            SireCodes = getAssetLocationsOrThrow(attributes, "sireCodes");
            spawnEntityCodes = getAssetLocationsOrThrow(attributes, "offspringCodes");

            if (attributes.KeyExists("gestationMonths")) {
                GestationDays = attributes["gestationMonths"].AsDouble() * entity.World.Calendar.DaysPerMonth;
            }
            else if (attributes.KeyExists("gestationDays")) {
                GestationDays = attributes["gestationDays"].AsDouble();
            }
            else {
                GestationDays = entity.World.Calendar.DaysPerMonth;
            }
            GestationDays *= AnimalConfig.AnimalGrowthTime;

            if (attributes.KeyExists("sireSearchRange")) {
                SireSearchRange = attributes["sireSearchRange"].AsFloat();
            }
            else {
                SireSearchRange = 16;
            }

            if (attributes.KeyExists("lactationMonths")) {
                LactationDays = attributes["lactationMonths"].AsDouble() * entity.World.Calendar.DaysPerMonth;
            }
            else if (attributes.KeyExists("lactationDays")) {
                LactationDays = attributes["lactationDays"].AsDouble();
            }
            LactationDays *= AnimalConfig.AnimalGrowthTime;

            if (attributes.KeyExists("breedingCooldownMonths")) {
                CooldownDays = attributes["breedingCooldownMonths"].AsDouble() * entity.World.Calendar.DaysPerMonth
                    * AnimalConfig.AnimalGrowthTime;
            }
            else if (attributes.KeyExists("breedingCooldownDays")) {
                CooldownDays = attributes["breedingCooldownDays"].AsDouble() * AnimalConfig.AnimalGrowthTime;
            }
            else {
                CooldownDays = LactationDays;
            }

            InducedOvulation = attributes["inducedOvulation"].AsBool(false);
            if (InducedOvulation) {
                EstrousCycleDays = CooldownDays;
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

            if (IsPregnant) {
                if (Litter == null) {
                    IsPregnant = false;
                    TotalDaysCooldownUntil = TotalDays + entity.World.Rand.NextDouble() * EstrousCycleDays;
                }
                else {
                    double length = TotalDaysPregnancyEnd - TotalDaysPregnancyStart;
                    if (length < 0.8 * GestationDays || length > 1.2 * GestationDays) {
                        double rate = 1 + 0.08 * (entity.World.Rand.NextDouble() - 0.5); // Random from 0.96 to 1.04
                        TotalDaysPregnancyEnd = TotalDaysPregnancyStart + rate * GestationDays;
                    }
                }
            }
            entity.Api.Event.EnqueueMainThreadTask( () =>
                listenerID = entity.World.RegisterGameTickListener(SlowTick, 24000), "register tick listener"
            );
        }

        protected override void PopulateSpawnEntityCodes() {
            // Do nothing, skip base class logic
        }

        protected AssetLocation[] getAssetLocationsOrThrow(JsonObject attributes, string key) {
            string[] strings = null;
            if (attributes.KeyExists(key)) {
                strings = attributes[key].AsArray<string>();
            }
            else {
                throw new FormatException("No " + key + " given for reproduce behavior of entity " + entity.Code);
            }
            AssetLocation[] ret = new AssetLocation[strings.Length];
            for (int i = 0; i < strings.Length; ++i) {
                ret[i] = AssetLocation.Create(strings[i], entity.Code.Domain);
            }
            return ret;
        }

        public override bool ShouldEat { get => true; }

        protected virtual void SlowTick(float dt) {
            if (!entity.World.Side.IsServer()) {
                return;
            }
            if (IsPregnant) {
                ProgressPregnancy();
            }
            else {
                ConsiderMating();
            }
        }

        protected void ConsiderMating() {
            if (entity.World.Rand.NextSingle() < 0.8f) {
                return;
            }

            if (!EntityCanMate(this.entity)) {
                return;
            }
            if (IsPregnant) {
                return;
            }

            if (TotalDaysCooldownUntil + DaysInHeat < TotalDays) {
                TotalDaysCooldownUntil += EstrousCycleDays;
            }
            if (TotalDaysCooldownUntil > TotalDays) {
                return;
            }

            if (!IsBreedingSeason()) {
                TotalDaysCooldownUntil += entity.World.Calendar.DaysPerMonth;
                return;
            }

            EntityBehaviorTaskAI taskAi = entity.GetBehavior<EntityBehaviorTaskAI>();
            if (taskAi == null) {
                entity.Api.Logger.Warning(Code + ": Entity with code " + entity.Code + " has no task ai behavior and will be unable to breed");
                return;
            }
            if (taskAi.TaskManager.ActiveTasksBySlot[0] is AiTaskMate) {
                // Already trying
                return;
            }

            Entity sire = GetSire();
            if (sire == null) {
                return;
            }

            AiTaskMate mateTask = new AiTaskMate((EntityAgent)entity, sire);
            mateTask.SetPriority(MateTaskPriority);
            taskAi.TaskManager.ExecuteTask(mateTask, 0);

            EntityBehaviorTaskAI sireTaskAi = sire.GetBehavior<EntityBehaviorTaskAI>();
            if (sireTaskAi == null) {
                entity.Api.Logger.Warning(Code + ": Potential sire entity with code " + sire.Code + " has no task ai behavior, this may cause difficulty for breeding");
                return;
            }
            if (!(sireTaskAi.TaskManager.ActiveTasksBySlot[0] is AiTaskMate)) {
                AiTaskMate sireMateTask = new AiTaskMate((EntityAgent)sire, entity);
                sireMateTask.SetPriority(MateTaskPriority);
                sireTaskAi.TaskManager.ExecuteTask(sireMateTask, 0);
            }
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

        public override void MateWith(Entity sire) {
            InEarlyPregnancy = true;
            TotalDaysPregnancyStart = TotalDays;
            double rate = 1 + 0.08 * (entity.World.Rand.NextDouble() - 0.5);
            TotalDaysPregnancyEnd = TotalDaysPregnancyStart + rate * GestationDays;

            base.MateWith(sire);
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

        protected virtual void ProgressPregnancy() {
            if (InEarlyPregnancy) {
                if (TotalDays > TotalDaysPregnancyStart + GestationDays / 8.0) {
                    EntityBehaviorGenetics gb = entity.GetBehavior<EntityBehaviorGenetics>();
                    if (gb != null) {
                        List<TreeAttribute> surviving = new List<TreeAttribute>();
                        foreach (TreeAttribute childTree in Litter.value) {
                            Genome childGenome = new Genome(gb.Genome.Type, childTree);
                            if (!childGenome.EmbryonicLethal()) {
                                surviving.Add(childTree);
                            }
                        }
                        if (surviving.Count == 0) {
                            SetNotPregnant();
                        }
                        else {
                            Litter.value = surviving.ToArray();
                            entity.WatchedAttributes.MarkPathDirty("multiply");
                        }
                    }
                    InEarlyPregnancy = false;
                }
                return;
            }
            if (TotalDays > TotalDaysPregnancyEnd) {
                GiveBirth();
            }
        }

        protected void GiveBirth() {
            int nextGeneration = NextGeneration();
            TotalDaysLastBirth = TotalDays;
            TotalDaysCooldownUntil = TotalDays + CooldownDays;
            TreeAttribute[] litterData = Litter?.value;
            foreach (TreeAttribute childData in litterData) {
                Entity spawn = SpawnNewborn(entity.World, entity.Pos, entity, nextGeneration, childData);
            }
            SetNotPregnant();
        }

        public static bool EntityCanMate(Entity entity) {
            if (!entity.Alive) {
                return false;
            }
            if (entity.WatchedAttributes.GetBool("neutered", false)) {
                return false;
            }
            if (!entity.MatingAllowed()) {
                return false;
            }
            double animalWeight = entity.BodyCondition();
            if (animalWeight <= DetailedHarvestable.MALNOURISHED || animalWeight > DetailedHarvestable.FAT) {
                return false;
            }
            return true;
        }

        protected virtual Entity GetSire() {
            Entity[] entities = entity.World.GetEntitiesAround(entity.Pos.XYZ, SireSearchRange, SireSearchRange,
                (e) => {
                    foreach (AssetLocation sire in SireCodes) {
                        if (e.WildCardMatch(sire) && EntityCanMate(e)) {
                            return true;
                        }
                    }
                    return false;
                }
            );
            if (entity.World.Rand.NextSingle() < 0.1f) {
                return entities[entity.World.Rand.Next(entities.Length)];
            }
            return ChooseAvoidingCloseRelatives(entity, entities);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn) {
            base.OnEntityDespawn(despawn);
            entity.Api.Event.EnqueueMainThreadTask( () =>
                entity.World.UnregisterGameTickListener(listenerID), "unregister tick listener"
            );
        }

        public override void GetInfoText(StringBuilder infotext) {
            if (!entity.Alive) {
                return;
            }
            multiplyTree = entity.WatchedAttributes.GetTreeAttribute("multiply");
            if (IsPregnant) {
                int passed = (int)Math.Round(TotalDays - TotalDaysPregnancyStart);
                int expected = (int)Math.Round(GestationDays);
                infotext.AppendLine(Lang.Get("detailedanimals:infotext-reproduce-pregnancy", passed, expected));
                if (InEarlyPregnancy) {
                    infotext.AppendLine(Lang.Get("detailedanimals:infotext-reproduce-earlypregnancy"));
                }
                else if (TotalDays > TotalDaysPregnancyStart + GestationDays * 2.0 / 3.0) {
                    infotext.AppendLine(Lang.Get("detailedanimals:infotext-reproduce-latepregnancy"));
                }
                return;
            }
            if (entity.WatchedAttributes.GetBool("neutered", false)) {
                return;
            }
            float animalWeight = entity.WatchedAttributes.GetFloat("animalWeight", 1);
            GetRemainingInfoText(infotext, animalWeight);
        }

        protected void GetRemainingInfoText(StringBuilder infotext, double animalWeight) {
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
                infotext.AppendLine(Lang.Get("detailedanimals:infotext-reproduce-waitdays" + Math.Ceiling(daysLeft).ToString()));
            }
            else {
                infotext.AppendLine(Lang.Get("game:Several days left before ready to mate"));
            }
        }

        public override string PropertyName() => Code;
    }
}
