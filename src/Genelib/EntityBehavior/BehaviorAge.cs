// Based on PetAI's BehaviorRaisable (MIT licensed), which is based on Vintage Story's BehaviorGrow
// Options for code reuse limited by the majority of the logic in BehaviorGrow hiding in a private non-virtual method

using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Genelib {
    public class BehaviorAge : EntityBehavior {
        public const string Code = "age";
        private const double coef = 1;

        private long? callbackID;
        private ITreeAttribute growTree;
        private double StartingWeight = 0.00001;
        protected float FinalWeight = 1;

        public AssetLocation AdultEntityCode { get; protected set; }
        public double HoursToGrow { get; protected set; }

        internal double TimeSpawned {
            get { return growTree.GetDouble("timeSpawned"); }
            set { growTree.SetDouble("timeSpawned", value); }
        }

        internal double GrowthPausedSince {
            get { return growTree.GetDouble("growthPausedSince", -1); }
            set { growTree.SetDouble("growthPausedSince", value); }
        }

        public float GrowthWeightFraction {
            get => entity.WatchedAttributes.GetFloat("growthWeightFraction", 1);
            set => entity.WatchedAttributes.SetFloat("growthWeightFraction", value);
        }

        public BehaviorAge(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes) {
            base.Initialize(properties, typeAttributes);

            if (typeAttributes.KeyExists("monthsToGrow")) {
                HoursToGrow = typeAttributes["monthsToGrow"].AsFloat() 
                    * entity.World.Calendar.DaysPerMonth * entity.World.Calendar.HoursPerDay;
            }
            else {
                HoursToGrow = typeAttributes["hoursToGrow"].AsFloat(96);
            }
            HoursToGrow *= GeneticsModSystem.Config.AnimalGrowthTime;

            growTree = entity.WatchedAttributes.GetTreeAttribute("grow");
            if (growTree == null) {
                entity.WatchedAttributes.SetAttribute("grow", growTree = new TreeAttribute());
                TimeSpawned = entity.World.Calendar.TotalHours;
            }

            if (typeAttributes.KeyExists("adultEntityCodes")) {
                string[] locations = typeAttributes["adultEntityCodes"].AsArray<string>(new string[0]);
                AdultEntityCode = new AssetLocation(locations[entity.EntityId % locations.Length]);
            }
            else if (typeAttributes.KeyExists("adultEntityCode")) {
                AdultEntityCode = new AssetLocation(typeAttributes["adultEntityCode"].AsString());
            }

            if (typeAttributes.KeyExists("initialWeight")) {
                StartingWeight = typeAttributes["initialWeight"].AsFloat();
            }

            if (typeAttributes.KeyExists("finalWeight")) {
                FinalWeight = typeAttributes["finalWeight"].AsFloat();
            }
            else if (AdultEntityCode != null) {
                EntityProperties adultType = entity.World.GetEntityType(AdultEntityCode);
                if (adultType.Attributes.KeyExists("initialWeight")) {
                    FinalWeight = adultType.Attributes["initialWeight"].AsFloat();
                }
            }

            if (!entity.WatchedAttributes.HasAttribute("growthWeightFraction")
                    || Double.IsNaN(entity.WatchedAttributes.GetFloat("growthWeightFraction"))) {
                GrowthWeightFraction = (float)ExpectedWeight((entity.World.Calendar.TotalHours - TimeSpawned) / HoursToGrow);
            }

            if (entity.Alive) {
                callbackID = entity.World.RegisterCallback(CheckGrowth, 6000);
            }
        }

        protected virtual double ExpectedWeight(double ageFraction) {
            double r = 1 - Math.Exp(-1 * coef);
            double n = -1 / coef * Math.Log(1 - StartingWeight / FinalWeight * r);
            double x = n + ageFraction * (1 - n);
            return FinalWeight * (1 - Math.Exp(-x * coef)) / r;
        }

        public override void OnEntityDeath(DamageSource damageSource) {
            GrowthPausedSince = entity.World.Calendar.TotalHours;
            UnregisterCallback();
        }

        public override void OnEntityRevive() {
            TimeSpawned += entity.World.Calendar.TotalHours - GrowthPausedSince;
            callbackID = entity.World.RegisterCallback(CheckGrowth, 24000);
        }

        protected virtual void CheckGrowth(float dt) {
            if (!entity.Alive) {
                return;
            }

            if (entity.World.Calendar.TotalHours >= TimeSpawned + HoursToGrow) {
                AttemptBecomingAdult();
            }
            else {
                double age = entity.World.Calendar.TotalHours - TimeSpawned;
                if (age >= 0.1 * HoursToGrow) {
                    // Used for drawing the critter larger over time if SizeGrowthFactor is nonzero
                    float newAge = (float)(age / HoursToGrow - 0.1);
                    if (newAge >= 1.01f * growTree.GetFloat("age")) {
                        growTree.SetFloat("age", newAge);
                        entity.WatchedAttributes.MarkPathDirty("grow");
                    }
                }
                double expected = Math.Max(ExpectedWeight(age / HoursToGrow), GrowthWeightFraction);
                float animalWeight = entity.WatchedAttributes.GetFloat("animalWeight", 1);
                float currentWeight = animalWeight * GrowthWeightFraction;
                GrowthWeightFraction = (float)expected;
                entity.WatchedAttributes.SetFloat("animalWeight", currentWeight / (float)expected);
                callbackID = entity.World.RegisterCallback(CheckGrowth, 24000);
            }

            entity.World.FrameProfiler.Mark("entity-checkgrowth");
        }

        protected virtual void AttemptBecomingAdult() {
            AssetLocation code = AdultEntityCode;
            if (code == null) {
                return;
            }

            EntityProperties adultType = entity.World.GetEntityType(code);

            if (adultType == null) {
                entity.World.Logger.Error("Misconfigured entity. Entity with code '{0}' is configured (via Grow behavior) to grow into '{1}', but no such entity type was registered.", entity.Code, code);
                return;
            }

            Cuboidf collisionBox = adultType.SpawnCollisionBox;

            // Delay adult spawning if we're colliding
            if (entity.World.CollisionTester.IsColliding(entity.World.BlockAccessor, collisionBox, entity.ServerPos.XYZ, false)) {
                callbackID = entity.World.RegisterCallback(CheckGrowth, 3000);
                return;
            }

            Entity adult = entity.World.ClassRegistry.CreateEntity(adultType);
            adult.ServerPos.SetFrom(entity.ServerPos);
            adult.Pos.SetFrom(adult.ServerPos);

            CopyAttributesTo(adult);
            entity.Die(EnumDespawnReason.Expire, null);
            entity.World.SpawnEntity(adult);
        }

        protected virtual void CopyAttributesTo(Entity adult) {
            adult.WatchedAttributes.SetInt("generation", entity.WatchedAttributes.GetInt("generation", 0));
            adult.WatchedAttributes.SetDouble("birthTotalDays", entity.World.Calendar.TotalDays); // Used for antler growth
            adult.GetBehavior<EntityBehaviorNameTag>()?.SetName(entity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName);
            CopyAttributeIfPresent(adult, "hunger");
            CopyAttributeIfPresent(adult, "animalWeight");

            bool keepTexture = adult.WatchedAttributes.HasAttribute("textureIndex") 
                && entity.Properties.Client?.FirstTexture?.Alternates != null 
                && adult.Properties.Client?.FirstTexture?.Alternates != null 
                && entity.Properties.Client.FirstTexture.Alternates.Length == adult.Properties.Client.FirstTexture.Alternates.Length;

            if (keepTexture) {
                //Attempt to not change the texture during growing up
                adult.WatchedAttributes.SetInt("textureIndex", entity.WatchedAttributes.GetInt("textureIndex", 0));
            }

            CopyAttributeIfPresent(adult, EntityBehaviorGenetics.Code);
            CopyAttributeIfPresent(adult, "motherId");
            CopyAttributeIfPresent(adult, "fatherId");

            // PetAI compat
            CopyAttributeIfPresent(adult, "domesticationstatus");
            /* TODO: Add this back if it can be done without creating a hard dependency on PetAI
            if (entity is EntityPet childPet && adult is EntityPet adultPet) {
                for (int i = 0; i < childPet.GearInventory.Count; i++) {
                    childPet.GearInventory[i].TryPutInto(entity.World, adultPet.GearInventory[i]);
                }
            }*/
        }

        protected void CopyAttributeIfPresent(Entity adult, string key) {
            if (entity.WatchedAttributes.HasAttribute(key)) {
                adult.WatchedAttributes.SetAttribute(key, entity.WatchedAttributes.GetAttribute(key));
            }
        }

        protected void UnregisterCallback() {
            if (callbackID != null) {
                entity.World.UnregisterCallback((long)callbackID);
                callbackID = null;
            }
        }

        public override void OnEntityDespawn(EntityDespawnData despawn) {
            UnregisterCallback();
        }


        public override string PropertyName() => Code;
    }
}
