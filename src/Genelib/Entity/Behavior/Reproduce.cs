using Genelib.Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
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
        protected enum BreedingSeason {
            Continuous,
            ShortDay,
            LongDay
        }
        public const string Code = "genelib.reproduce";
        private const float DEFAULT_WEIGHT = 0.04f;

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
        protected BreedingSeason Season = BreedingSeason.Continuous;
        protected double litterAddChance = 0;
        protected int litterAddAttempts = 0;
        protected bool InducedOvulation = false;
        public CollectibleObject[] EggTypes;
        public bool LaysEggs => EggTypes != null;

        public double SynchedTotalDaysCooldownUntil
        {
            get { return multiplyTree.GetDouble("totalDaysCooldownUntil"); }
            set {
                multiplyTree.SetDouble("totalDaysCooldownUntil", value);
                entity.WatchedAttributes.MarkPathDirty("multiply");
            }
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

            // TODO: Add a way to customize this per-entity from WatchedAttributes or similar
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

            if (attributes.KeyExists("breedingSeason")) {
                string breedingSeason = attributes["breedingSeason"].AsString();
                if (breedingSeason.Equals("longday")) {
                    Season = BreedingSeason.LongDay;
                }
                else if (breedingSeason.Equals("shortday")) {
                    Season = BreedingSeason.ShortDay;
                }
                else if (breedingSeason.Equals("continuous")) {
                    Season = BreedingSeason.Continuous;
                }
                else {
                    entity.World.Logger.Warning("Unable to parse breedingSeason value of \"" + breedingSeason + "\" for entity " + entity.Code);
                }
            }

            if (attributes.KeyExists("litterAddChance")) {
                litterAddChance = attributes["litterAddChance"].AsDouble();
            }
            if (attributes.KeyExists("litterAddAttempts")) {
                litterAddAttempts = attributes["litterAddAttempts"].AsInt();
            }

            if (!entity.World.Side.IsServer()) {
                // Do not add multiply tree client-side or it won't sync
                return;
            }
            multiplyTree = entity.WatchedAttributes.GetOrAddTreeAttribute("multiply");

            if (IsPregnant && Litter == null) {
                IsPregnant = false;
                SynchedTotalDaysCooldownUntil = TotalDays + entity.World.Rand.NextDouble() * EstrousCycleDays;
            }
            listenerID = entity.World.RegisterGameTickListener(SlowTick, 24000);
        }

        private AssetLocation[] getAssetLocationsOrThrow(JsonObject attributes, string key) {
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

        protected void SlowTick(float dt) {
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

            if (SynchedTotalDaysCooldownUntil + DaysInHeat < TotalDays) {
                SynchedTotalDaysCooldownUntil += EstrousCycleDays;
            }
            if (SynchedTotalDaysCooldownUntil > TotalDays) {
                return;
            }

            if (!isBreedingSeason()) {
                SynchedTotalDaysCooldownUntil += entity.World.Calendar.DaysPerMonth;
                return;
            }

            Entity sire = GetSire();
            if (sire == null) {
                return;
            }
            // TODO: Attempt to pathfind to the sire
            IsPregnant = true;
            InEarlyPregnancy = true;
            TotalDaysPregnancyStart = TotalDays;
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
                litterData.value[i].SetLong("fatherId", sire.EntityId);
            }
            Litter = litterData;
        }

        private bool isBreedingSeason() {
            if (Season == BreedingSeason.LongDay || Season == BreedingSeason.ShortDay) {
                float season = entity.World.Calendar.GetSeasonRel(entity.Pos.AsBlockPos);
                if (Season == BreedingSeason.ShortDay) {
                    season = (season + 0.5f) % 1f;
                }
                float months = 12;
                double vernal_equinox = 2.66 / months;
                double summer_solstice = 5.66 / months;
                return season >= vernal_equinox && season < summer_solstice;
            }
            return true;
        }

        // If the animal dies, you lose the pregnancy even if you later revive it
        public override void OnEntityDeath(DamageSource damageSource) {
            SetNotPregnant();
        }

        // And on revival, if not pregnant, you do not progress towards becoming so
        public override void OnEntityRevive() {
            SynchedTotalDaysCooldownUntil += (entity.World.Calendar.TotalHours - GrowthPausedSince) / 24.0;
        }

        protected void ProgressPregnancy() {
            if (LaysEggs) {
                if (TotalDays > TotalDaysPregnancyStart + GestationDays) {
                    SetNotPregnant();
                }
                return;
            }
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
            if (TotalDays > TotalDaysPregnancyStart + GestationDays) {
                GiveBirth();
            }
        }

        protected void GiveBirth() {
            int nextGeneration = NextGeneration();
            TotalDaysLastBirth = TotalDays;
            SynchedTotalDaysCooldownUntil = TotalDays + CooldownDays;
            TreeAttribute[] litterData = Litter?.value;
            foreach (TreeAttribute childData in litterData) {
                Entity spawn = SpawnNewborn(entity, nextGeneration, childData);
            }
            SetNotPregnant();
        }

        public static Entity SpawnNewborn(Entity entity, int nextGeneration, TreeAttribute childData) {
            AssetLocation spawnCode = new AssetLocation(childData.GetString("code"));
            EntityProperties spawnType = entity.World.GetEntityType(spawnCode);
            if (spawnType == null) {
                throw new ArgumentException(entity.Code.ToString() + " attempted to hatch or give birth to entity with code " 
                    + spawnCode.ToString() + ", but no such entity was found.");
            }
            Entity spawn = entity.World.ClassRegistry.CreateEntity(spawnType);
            spawn.ServerPos.SetFrom(entity.ServerPos);
            Random random = entity.World.Rand;
            spawn.ServerPos.Motion.X += (random.NextDouble() - 0.5f) / 20f;
            spawn.ServerPos.Motion.Z += (random.NextDouble() - 0.5f) / 20f;
            spawn.Pos.SetFrom(spawn.ServerPos);
            spawn.Attributes.SetString("origin", "reproduction");
            spawn.WatchedAttributes.SetInt("generation", nextGeneration);
            spawn.WatchedAttributes.SetLong("fatherId", childData.GetLong("fatherId"));
            spawn.WatchedAttributes.SetLong("motherId", childData.GetLong("motherId", entity.EntityId));
            spawn.WatchedAttributes.SetLong("fosterId", entity.EntityId);
            if (childData.HasAttribute("genetics")) {
                spawn.WatchedAttributes.SetAttribute("genetics", childData.GetTreeAttribute("genetics"));
            }
            spawn.WatchedAttributes.SetDouble("birthTotalDays", entity.World.Calendar.TotalDays);

            entity.World.SpawnEntity(spawn);
            return spawn;
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

            AnimalHunger hunger = entity.GetBehavior<AnimalHunger>();
            if (hunger != null) {
                float theRestOfTheWeight = entity.Properties.Attributes["adultWeightKg"].AsFloat() * entity.WeightModifierExceptCondition();
                double prevTotalWeight = hunger.BodyCondition * theRestOfTheWeight;
                double newTotalWeight = Math.Max(prevTotalWeight * 0.1f, prevTotalWeight - eggWeight);
                hunger.BodyCondition = newTotalWeight / theRestOfTheWeight;
            }

            ItemStack eggStack = new ItemStack(egg);
            TreeAttribute chick = PopChild();
            if (chick == null) {
                return eggStack;
            }

            EntityBehaviorGenetics gb = entity.GetBehavior<EntityBehaviorGenetics>();
            if (gb != null) {
                Genome childGenome = new Genome(gb.Genome.Type, chick);
                if (childGenome.EmbryonicLethal()) {
                    return eggStack;
                }
            }
            chick.SetInt("generation", NextGeneration());
            eggStack.Attributes["chick"] = chick;

            return eggStack;
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
            float animalWeight = entity.WatchedAttributes.GetFloat("animalWeight", 1);
            if (animalWeight <= DetailedHarvestable.MALNOURISHED || animalWeight > DetailedHarvestable.FAT) {
                return false;
            }
            return true;
        }

        protected virtual Entity GetSire() {
            return entity.World.GetNearestEntity(entity.ServerPos.XYZ, SireSearchRange, SireSearchRange,
                (e) => {
                    foreach (AssetLocation sire in SireCodes) {
                        if (e.WildCardMatch(sire) && EntityCanMate(e)) {
                            return true;
                        }
                    }
                    return false;
                }
            );
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
                if (LaysEggs) {
                    infotext.AppendLine(Lang.Get("game:Ready to lay"));
                    return;
                }
                if (InEarlyPregnancy) {
                    infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-earlypregnancy"));
                }
                else if (TotalDays > TotalDaysPregnancyStart + GestationDays * 2.0 / 3.0) {
                    infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-latepregnancy"));
                }
                else {
                    infotext.AppendLine(Lang.Get("Is pregnant"));
                }
                return;
            }
            if (entity.WatchedAttributes.GetBool("neutered", false)) {
                return;
            }
            if (!entity.MatingAllowed()) {
                return;
            }
            float animalWeight = entity.WatchedAttributes.GetFloat("animalWeight", 1);
            if (animalWeight <= DetailedHarvestable.MALNOURISHED) {
                infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-underweight"));
                return;
            }
            else if (animalWeight > DetailedHarvestable.FAT) {
                infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-overweight"));
                return;
            }

            if (!isBreedingSeason()) {
                if (Season == BreedingSeason.LongDay) {
                    infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-longday"));
                }
                else if (Season == BreedingSeason.ShortDay) {
                    infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-shortday"));
                }
                return;
            }

            double daysLeft = SynchedTotalDaysCooldownUntil - TotalDays;
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
