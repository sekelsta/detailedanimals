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

namespace Genelib {
    public class Reproduce : EntityBehaviorMultiply {
        public const string Code = "genelib.reproduce";

        // TODO: rearrange to handle hybrids - e.g., offspringBySire
        protected AssetLocation[] SireCodes;
        protected AssetLocation[] OffspringCodes;
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

        protected TreeArrayAttribute Litter {
            get => multiplyTree["litter"] as TreeArrayAttribute;
            set { 
                multiplyTree["litter"] = value;
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

        public void SetNotPregnant() {
            IsPregnant = false;
            multiplyTree.RemoveAttribute("litter");
            entity.WatchedAttributes.MarkPathDirty("multiply");
        }

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
            OffspringCodes = getAssetLocationsOrThrow(attributes, "offspringCodes");

            if (attributes.KeyExists("gestationMonths")) {
                GestationDays = attributes["gestationMonths"].AsDouble() * entity.World.Calendar.DaysPerMonth;
            }
            else if (attributes.KeyExists("gestationDays")) {
                GestationDays = attributes["gestationDays"].AsDouble();
            }
            else {
                GestationDays = entity.World.Calendar.DaysPerMonth;
            }
            GestationDays *= GenelibSystem.AnimalGrowthTime;

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
            LactationDays *= GenelibSystem.AnimalGrowthTime;

            if (attributes.KeyExists("breedingCooldownMonths")) {
                CooldownDays = attributes["breedingCooldownMonths"].AsDouble() * entity.World.Calendar.DaysPerMonth
                    * GenelibSystem.AnimalGrowthTime;
            }
            else if (attributes.KeyExists("breedingCooldownDays")) {
                CooldownDays = attributes["breedingCooldownDays"].AsDouble() * GenelibSystem.AnimalGrowthTime;
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
            listenerID = entity.World.RegisterGameTickListener(SlowTick, 24000);
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

        public void MateWith(Entity sire) {
            IsPregnant = true;
            InEarlyPregnancy = true;
            TotalDaysPregnancyStart = TotalDays;
            double rate = 1 + 0.08 * (entity.World.Rand.NextDouble() - 0.5);
            TotalDaysPregnancyEnd = TotalDaysPregnancyStart + rate * GestationDays;
            Genome sireGenome = sire.GetBehavior<EntityBehaviorGenetics>()?.Genome;
            Genome ourGenome = entity.GetBehavior<EntityBehaviorGenetics>()?.Genome;

            int litterSize = 1;
            for (int i = 0; i < litterAddAttempts; ++i) {
                if (entity.World.Rand.NextDouble() < litterAddChance) {
                    litterSize += 1;
                }
            }

            TreeArrayAttribute litterData = new TreeArrayAttribute();
            litterData.value = new TreeAttribute[litterSize];
            for (int i = 0; i < litterSize; ++i) {
                AssetLocation offspringCode = OffspringCodes[entity.World.Rand.Next(OffspringCodes.Length)];
                litterData.value[i] = new TreeAttribute();
                if (ourGenome != null && sireGenome != null) {
                    bool heterogametic = ourGenome.Type.SexDetermination.Heterogametic(entity.IsMale());
                    Genome child = new Genome(ourGenome, sireGenome, heterogametic, entity.World.Rand);
                    child.Mutate(GenelibSystem.MutationRate, entity.World.Rand);
                    TreeAttribute childGeneticsTree = (TreeAttribute) litterData.value[i].GetOrAddTreeAttribute("genetics");
                    child.AddToTree(childGeneticsTree);
                }
                litterData.value[i].SetString("code", offspringCode.ToString());

                litterData.value[i].SetLong("motherId", entity.UniqueID());
                string motherName = entity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
                if (motherName != null && motherName != "") {
                    litterData.value[i].SetString("motherName", motherName);
                }
                litterData.value[i].SetString("motherKey", entity.Code.Domain + ":item-creature-" + entity.Code.Path);

                litterData.value[i].SetLong("fatherId", sire.UniqueID());
                string fatherName = sire.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
                if (fatherName != null && fatherName != "") {
                    litterData.value[i].SetString("fatherName", fatherName);
                }
                litterData.value[i].SetString("fatherKey", sire.Code.Domain + ":item-creature-" + sire.Code.Path);
            }
            Litter = litterData;
        }

        public bool IsBreedingSeason() {
            if (!SeasonalBreeding || BreedingSeasonBefore + BreedingSeasonAfter >= 1) {
                return true;
            }
            float season = entity.World.Calendar.GetSeasonRel(entity.Pos.AsBlockPos);
            if (season > BreedingSeasonPeak) {
                season -= 1;
            }
            double timeUntilPeak = BreedingSeasonPeak - season;
            return timeUntilPeak < BreedingSeasonBefore || 1 - timeUntilPeak < BreedingSeasonAfter;
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

        public static Entity SpawnNewborn(IWorldAccessor world, EntityPos pos, Entity foster, int nextGeneration, TreeAttribute childData) {
            AssetLocation spawnCode = new AssetLocation(childData.GetString("code"));
            EntityProperties spawnType = world.GetEntityType(spawnCode);
            if (spawnType == null) {
                throw new ArgumentException(foster?.Code.ToString() + " attempted to hatch or give birth to entity with code " 
                    + spawnCode.ToString() + ", but no such entity was found.");
            }
            Entity spawn = world.ClassRegistry.CreateEntity(spawnType);
            spawn.ServerPos.SetFrom(pos);
            spawn.ServerPos.Yaw = world.Rand.NextSingle() * GameMath.TWOPI;
            Random random = world.Rand;
            spawn.ServerPos.Motion.X += (random.NextDouble() - 0.5f) / 20f;
            spawn.ServerPos.Motion.Z += (random.NextDouble() - 0.5f) / 20f;
            spawn.Pos.SetFrom(spawn.ServerPos);
            spawn.Attributes.SetString("origin", "reproduction");
            spawn.WatchedAttributes.SetInt("generation", nextGeneration);
            // Alternately, call childData.RemoveAttribute("code"), then copy over all remaining attributes
            spawn.WatchedAttributes.SetLong("fatherId", childData.GetLong("fatherId"));
            spawn.WatchedAttributes.CopyIfPresent("fatherName", childData);
            spawn.WatchedAttributes.CopyIfPresent("fatherKey", childData);
            spawn.WatchedAttributes.SetLong("motherId", childData.GetLong("motherId"));
            spawn.WatchedAttributes.CopyIfPresent("motherName", childData);
            spawn.WatchedAttributes.CopyIfPresent("motherKey", childData);
            spawn.SetFoster(foster);
            spawn.WatchedAttributes.CopyIfPresent("genetics", childData);
            spawn.WatchedAttributes.SetDouble("birthTotalDays", world.Calendar.TotalDays);

            world.SpawnEntity(spawn);
            return spawn;
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
            if (entities == null || entities.Length == 0) {
                return null;
            }
            Entity best = entities[0];
            bool closeRelative = entity.IsCloseRelative(best);
            float distance = entity.Pos.SquareDistanceTo(best.Pos);
            for (int i = 1; i < entities.Length; ++i) {
                if (closeRelative && !entity.IsCloseRelative(entities[i])) {
                    best = entities[i];
                    closeRelative = false;
                    continue;
                }
                float currentDistance = entity.Pos.SquareDistanceTo(entities[i].Pos);
                if (distance > currentDistance) {
                    best = entities[i];
                    distance = currentDistance;
                }
            }
            return best;
        }

        public override void OnEntityDespawn(EntityDespawnData despawn) {
            base.OnEntityDespawn(despawn);
            entity.World.UnregisterGameTickListener(listenerID);
        }

        public override void GetInfoText(StringBuilder infotext) {
            if (!entity.Alive) {
                return;
            }
            multiplyTree = entity.WatchedAttributes.GetTreeAttribute("multiply");
            if (IsPregnant) {
                int passed = (int)Math.Round(TotalDays - TotalDaysPregnancyStart);
                int expected = (int)Math.Round(GestationDays);
                infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-pregnancy", passed, expected));
                if (InEarlyPregnancy) {
                    infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-earlypregnancy"));
                }
                else if (TotalDays > TotalDaysPregnancyStart + GestationDays * 2.0 / 3.0) {
                    infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-latepregnancy"));
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
                infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-underweight"));
                return;
            }
            else if (animalWeight > DetailedHarvestable.FAT) {
                infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-overweight"));
                return;
            }

            if (!IsBreedingSeason()) {
                double breedingStart = (BreedingSeasonPeak - BreedingSeasonBefore + 1) % 1;
                if (breedingStart < 0.5) {
                    infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-longday"));
                }
                else {
                    infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-shortday"));
                }
                return;
            }

            double daysLeft = TotalDaysCooldownUntil - TotalDays;
            if (daysLeft <= 0) {
                infotext.AppendLine(Lang.Get("game:Ready to mate"));
            }
            else if (daysLeft <= 4) {
                infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-waitdays" + Math.Ceiling(daysLeft).ToString()));
            }
            else {
                infotext.AppendLine(Lang.Get("game:Several days left before ready to mate"));
            }
        }

        public override string PropertyName() => Code;
    }
}
