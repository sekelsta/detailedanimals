using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Genelib {
    public class Reproduce : EntityBehavior {
        public const string Code = "reproduce";

        protected AssetLocation[] SireCodes;
        protected float SireSearchRange;
        protected long listenerID;
        protected double CooldownMonths;

        // Data shared with vanilla multiply behavior
        protected ITreeAttribute multiplyTree;
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

        public long SireID {
            get => multiplyTree.GetLong("sireId");
            set => multiplyTree.SetLong("sireId", value);
        }
        public Genome SireGenome { get; set; } // TODO

        // Calendar.TotalDays includes timelapse adjustment, Calendar.TotalHours does not
        public virtual double TotalDays {
            get => entity.World.Calendar.TotalHours / 24.0;
        }

        public Reproduce(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject attributes) {
            string[] sireCodeStrings = attributes["sireCodes"].AsArray<string>();
            SireCodes = new AssetLocation[sireCodeStrings.Length];
            for (int i = 0; i < sireCodeStrings.Length; ++i) {
                SireCodes[i] = AssetLocation.Create(sireCodeStrings[i], entity.Code.Domain);
            }
            string[] offspringCodes = attributes["offspringCodes"].AsArray<string>();
            float gestationMonths = attributes["gestationMonths"].AsFloat();
            cooldownMonths = attributes["breedingCooldownMonths"].AsDouble();
            SireSearchRange = attributes["sireSearchRange"].AsFloat();

            multiplyTree = entity.WatchedAttributes.GetOrAddTreeAttribute("multiply");
            if (IsPregnant && SireGenome == null && entity.HasBehavior<Genetics>()) {
                SireGenome = null;// TODO
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
            if (TotalDaysCooldownUntil > TotalDays) {
                return;
            }
            // TODO: Return if not in heat
            // TODO: Return if too crowded

            Entity sire = GetSire();
            if (sire == null) {
                return;
            }
            // TODO: Attempt to pathfind to the sire
            IsPregnant = true;
            TotalDaysPregnancyStart = TotalDays;
            SireID = sire.EntityId;
            Genome sireGenome = sire.GetBehavior<Genetics>()?.Genome;
            if (sireGenome != null) {
                SireGenome = sireGenome;
            }
            Genome ourGenome = entity.GetBehavior<Genetics>()?.Genome;
            // TOOD: Pick litter size
            // TOOD: Create offspring genomes (or this can wait until miscarriage time?)
        }

        protected void ProgressPregnancy() {
            // TODO
        }

        protected void GiveBirth() {
            TotalDaysLastBirth = TotalDays;
            TotalDaysCooldownUntil = TotalDays + CooldownMonths * entity.World.Calendar.DaysPerMonth;
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
            infotext.AppendLine("EntityBehavior Reproduce");
        }

        public override string PropertyName() => Code;
    }
}
