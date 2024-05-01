using Genelib.Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Genelib {
    public class Reproduce : EntityBehavior {
        protected enum BreedingSeason {
            Continuous,
            InducedOvulation,
            FallAndWinter,
            SpringAndSummer
        }
        public const string Code = "reproduce";

        protected AssetLocation[] SireCodes;
        protected AssetLocation[] OffspringCodes;
        protected float SireSearchRange;
        protected long listenerID;
        protected double CooldownDays;
        protected double GestationDays;
        protected double LactationDays;
        protected double EstrousCycleDays;
        protected double DaysInHeat;
        protected BreedingSeason Season = BreedingSeason.Continuous;

        // Data shared with vanilla multiply behavior
        protected TreeAttribute multiplyTree;
        public double TotalDaysCooldownUntil {
            get => multiplyTree.GetDouble("totalDaysCooldownUntil");
            set => multiplyTree.SetDouble("totalDaysCooldownUntil", value);
        }
        public bool IsPregnant {
            get => multiplyTree.GetBool("isPregnant");
            set => multiplyTree.SetBool("isPregnant", value);
        }
        public double TotalDaysLastBirth {
            get => multiplyTree.GetDouble("totalDaysLastBirth", -9999);
            set => multiplyTree.SetDouble("totalDaysLastBirth", value);
        }
        public double TotalDaysPregnancyStart {
            get => multiplyTree.GetDouble("totalDaysPregnancyStart");
            set => multiplyTree.SetDouble("totalDaysPregnancyStart", value);
        }
        // End shared vanilla multiply data

        public bool InEarlyPregnancy {
            get => multiplyTree.GetBool("earlyPregnancy", true);
            set => multiplyTree.SetBool("earlyPregnancy", value);
        }
        public double GrowthPausedSince {
            get => entity.WatchedAttributes.GetTreeAttribute("grow")?.GetDouble("growthPausedSince", -1) ?? entity.World.Calendar.TotalHours;
        }

        // Calendar.TotalDays includes timelapse adjustment, Calendar.TotalHours does not
        public virtual double TotalDays {
            get => entity.World.Calendar.TotalHours / 24.0;
        }

        public Reproduce(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject attributes) {
            if (entity.World.Side == EnumAppSide.Server) {
                string[] sireCodeStrings = null;
                if (attributes.KeyExists("sireCodes")) {
                    sireCodeStrings = attributes["sireCodes"].AsArray<string>();
                }
                else if (attributes.KeyExists("requiresNearbyEntityCodes")) {
                    sireCodeStrings = attributes["requiresNearbyEntityCodes"].AsArray<string>();
                }
                else if (attributes.KeyExists("requiresNearbyEntityCode")) {
                    sireCodeStrings = new string[] { attributes["requiresNearbyEntityCode"].AsString() };
                }
                else {
                    throw new FormatException("No sireCodes given for reproduce behavior of entity " + entity.Code);
                }
                SireCodes = new AssetLocation[sireCodeStrings.Length];
                for (int i = 0; i < sireCodeStrings.Length; ++i) {
                    SireCodes[i] = AssetLocation.Create(sireCodeStrings[i], entity.Code.Domain);
                }

                string[] offspringCodeStrings = null;
                if (attributes.KeyExists("offspringCodes")) {
                    offspringCodeStrings = attributes["offspringCodes"].AsArray<string>();
                }
                else if (attributes.KeyExists("spawnEntityCodes")) {
                    offspringCodeStrings = attributes["spawnEntityCodes"].AsArray<string>();
                }
                else if (attributes.KeyExists("spawnEntityCode")) {
                    offspringCodeStrings = new string[] { attributes["spawnEntityCode"].AsString() };
                }
                else {
                    throw new FormatException("No offspringCodes given for reproduce behavior of entity " + entity.Code);
                }
                OffspringCodes = new AssetLocation[offspringCodeStrings.Length];
                for (int i = 0; i < offspringCodeStrings.Length; ++i) {
                    OffspringCodes[i] = AssetLocation.Create(offspringCodeStrings[i], entity.Code.Domain);
                }
            }

            if (attributes.KeyExists("gestationMonths")) {
                GestationDays = attributes["gestationMonths"].AsDouble() * entity.World.Calendar.DaysPerMonth;
            }
            else if (attributes.KeyExists("gestationDays")) {
                GestationDays = attributes["gestationDays"].AsDouble();
            }
            else {
                GestationDays = entity.World.Calendar.DaysPerMonth;
            }
            GestationDays *= GeneticsModSystem.Config.AnimalGrowthTime;

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
            else {
                LactationDays = entity.World.Calendar.DaysPerMonth;
            }
            LactationDays *= GeneticsModSystem.Config.AnimalGrowthTime;

            if (attributes.KeyExists("breedingCooldownMonths")) {
                CooldownDays = attributes["breedingCooldownMonths"].AsDouble() * entity.World.Calendar.DaysPerMonth
                    * GeneticsModSystem.Config.AnimalGrowthTime;
            }
            else if (attributes.KeyExists("breedingCooldownDays")) {
                CooldownDays = attributes["breedingCooldownDays"].AsDouble() * GeneticsModSystem.Config.AnimalGrowthTime;
            }
            else {
                CooldownDays = LactationDays;
            }

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

            if (attributes.KeyExists("breedingSeason")) {
                string breedingSeason = attributes["breedingSeason"].AsString();
                if (breedingSeason.Equals("longday") || breedingSeason.Equals("springsummer")) {
                    Season = BreedingSeason.SpringAndSummer;
                }
                else if (breedingSeason.Equals("shortday") || breedingSeason.Equals("fallwinter")) {
                    Season = BreedingSeason.FallAndWinter;
                }
                else if (breedingSeason.Equals("inducedovulation")) {
                    Season = BreedingSeason.InducedOvulation;
                }
                else if (breedingSeason.Equals("continuous")) {
                    Season = BreedingSeason.Continuous;
                }
                else {
                    entity.World.Logger.Warning("Unable to parse breedingSeason value of \"" + breedingSeason + "\" for entity " + entity.Code);
                }
            }

            multiplyTree = (TreeAttribute) entity.WatchedAttributes.GetOrAddTreeAttribute("multiply");
            if (IsPregnant && multiplyTree["litter"] == null) {
                IsPregnant = false;
                TotalDaysCooldownUntil = TotalDays + entity.World.Rand.NextDouble() * EstrousCycleDays;
            }
            listenerID = entity.World.RegisterGameTickListener(SlowTick, 24000);
        }

        protected void SlowTick(float dt) {
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
            if (TotalDaysCooldownUntil < TotalDays + DaysInHeat) {
                TotalDaysCooldownUntil += EstrousCycleDays;
            }
            if (TotalDaysCooldownUntil > TotalDays) {
                return;
            }
            // TODO: Seasonal breeding
            // TODO: Return if too crowded

            Entity sire = GetSire();
            if (sire == null) {
                return;
            }
            // TODO: Attempt to pathfind to the sire
            IsPregnant = true;
            InEarlyPregnancy = true;
            TotalDaysPregnancyStart = TotalDays;
            Genome sireGenome = sire.GetBehavior<Genetics>()?.Genome;
            Genome ourGenome = entity.GetBehavior<Genetics>()?.Genome;
            // TOOD: Pick litter size
            int litterSize = 3;
            TreeArrayAttribute litterData = new TreeArrayAttribute();
            litterData.value = new TreeAttribute[litterSize];
            for (int i = 0; i < litterSize; ++i) {
                AssetLocation offspringCode = OffspringCodes[entity.World.Rand.Next(OffspringCodes.Length)];
                bool heterogameic = entity.World.Rand.NextBool(); // TODO: Pick heterogameic based on spawn code
                Genome child = new Genome(ourGenome, sireGenome, heterogameic, entity.World.Rand);
                child.Mutate(GeneticsModSystem.MutationRate, entity.World.Rand);
                litterData.value[i] = new TreeAttribute();
                TreeAttribute childGeneticsTree = (TreeAttribute) litterData.value[i].GetOrAddTreeAttribute(Genetics.Code);
                child.AddToTree(childGeneticsTree);
                litterData.value[i].SetString("code", offspringCode.ToString());
                litterData.value[i].SetLong("sireId", sire.EntityId);
            }
            multiplyTree["litter"] = litterData;
        }

        // If the animal dies, you lose the pregnancy even if you later revive it
        public override void OnEntityDeath(DamageSource damageSource) {
            IsPregnant = false;
            multiplyTree["litter"] = null;
        }

        // And on revival, if not pregnant, you do not progress towards becoming so
        public override void OnEntityRevive() {
            TotalDaysCooldownUntil += (entity.World.Calendar.TotalHours - GrowthPausedSince) / 24.0;
        }

        protected void ProgressPregnancy() {
            if (InEarlyPregnancy) {
                if (TotalDays > TotalDaysPregnancyStart + GestationDays / 8.0) {
                    // TODO: Miscarry offspring with developmentally lethal genes
                    // TODO: If no offspring left, set IsPregnant to false
                    InEarlyPregnancy = false;
                }
                return;
            }
            if (TotalDays > TotalDaysPregnancyStart + GestationDays) {
                GiveBirth();
                IsPregnant = false;
            }
        }

        protected void GiveBirth() {
            int nextGeneration = entity.WatchedAttributes.GetInt("generation", 0) + 1;
            TotalDaysLastBirth = TotalDays;
            TotalDaysCooldownUntil = TotalDays + CooldownDays;
            Random random = entity.World.Rand;
            TreeAttribute[] litterData = (multiplyTree["litter"] as TreeArrayAttribute)?.value;
            foreach (TreeAttribute childData in litterData) {
                AssetLocation spawnCode = new AssetLocation(childData.GetString("code"));
                EntityProperties spawnType = entity.World.GetEntityType(spawnCode);
                if (spawnType == null) {
                    entity.World.Logger.Error(entity.Code.ToString() + " attempted to give birth to entity with code " 
                        + spawnCode.ToString() + ", but no such entity was found.");
                    continue;
                }
                Entity spawn = entity.World.ClassRegistry.CreateEntity(spawnType);
                spawn.ServerPos.SetFrom(entity.ServerPos);
                spawn.ServerPos.Motion.X += (random.NextDouble() - 0.5f) / 20f;
                spawn.ServerPos.Motion.Z += (random.NextDouble() - 0.5f) / 20f;
                spawn.Pos.SetFrom(spawn.ServerPos);

                spawn.Attributes.SetString("origin", "reproduction");
                spawn.WatchedAttributes.SetInt("generation", nextGeneration);
                spawn.WatchedAttributes.SetLong("motherId", entity.EntityId);
                spawn.WatchedAttributes.SetLong("fatherId", childData.GetLong("sireId"));
                if (childData.HasAttribute(Genetics.Code)) {
                    spawn.WatchedAttributes.SetAttribute(Genetics.Code, childData.GetTreeAttribute(Genetics.Code));
                }

                entity.World.SpawnEntity(spawn);
            }
            IsPregnant = false;
            multiplyTree.RemoveAttribute("litter");
        }

        public static bool EntityCanMate(Entity entity) {
            if (!entity.Alive) {
                return false;
            }
            if (entity.WatchedAttributes.GetBool("neutered", false)) {
                return false;
            }
            if (entity.WatchedAttributes.HasAttribute("domesticationstatus")) {
                if (!entity.WatchedAttributes.GetTreeAttribute("domesticationstatus").GetBool("multiplyAllowed", true)) {
                    return false;
                }
            }
            float animalWeight = entity.WatchedAttributes.GetFloat("animalWeight", 1);
            if (animalWeight < DetailedHarvestable.MinReproductionWeight
                    || animalWeight > DetailedHarvestable.MaxReproductionWeight) {
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
            base.GetInfoText(infotext);
            if (!entity.Alive) {
                return;
            }
            if (IsPregnant) {
                if (InEarlyPregnancy) {
                    infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-earlypregnancy"));
                }
                else if (TotalDays > TotalDaysPregnancyStart + GestationDays * 2.0 / 3.0) {
                    infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-latepregnancy"));
                }
                else {
                    infotext.AppendLine(Lang.Get("Is pregnant"));
                }
            }
            if (entity.WatchedAttributes.GetBool("neutered", false)) {
                return;
            }
            if (entity.WatchedAttributes.HasAttribute("domesticationstatus")) {
                if (!entity.WatchedAttributes.GetTreeAttribute("domesticationstatus").GetBool("multiplyAllowed", true)) {
                    return;
                }
            }
            float animalWeight = entity.WatchedAttributes.GetFloat("animalWeight", 1);
            if (animalWeight < DetailedHarvestable.MinReproductionWeight) {
                infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-underweight"));
                return;
            }
            else if (animalWeight > DetailedHarvestable.MaxReproductionWeight) {
                infotext.AppendLine(Lang.Get("genelib:infotext-reproduce-overweight"));
                return;
            }

            // TODO: If it is the wrong season, say so
            // TODO: If currently in heat, say so
            // Otherwise, say how long until it's time
            infotext.AppendLine("EntityBehavior Reproduce");
        }

        public override string PropertyName() => Code;
    }
}
