// Initially based on PetAI's BehaviorRaisable (MIT licensed), which is based on Vintage Story's BehaviorGrow

using Genelib.Extensions;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Genelib {
    public class BehaviorAge : EntityBehavior {
        public const string Code = "genelib.age";
        private const float secondsPerUpdate = 24;

        private long? callbackID;
        private ITreeAttribute growTree;
        private double StartingWeight = 0.00001;
        protected float FinalWeight = 1;
        protected float MaxGrowthScale;
        protected double maxGrowth;

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
            set {
                if (float.IsNaN(value)) {
                    throw new ArgumentException("Cannot set growth weight fraction to NaN. Entity code: " + entity.Code);
                }
                entity.WatchedAttributes.SetFloat("growthWeightFraction", value);
            }
        }

        public BehaviorAge(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes) {
            base.Initialize(properties, typeAttributes);

            if (entity.World.Side == EnumAppSide.Client) {
                entity.WatchedAttributes.RegisterModifiedListener("renderScale", ClientUpdateScale);
                ClientUpdateScale();
                return;
            }

            if (typeAttributes.KeyExists("monthsToGrow")) {
                HoursToGrow = typeAttributes["monthsToGrow"].AsFloat() 
                    * entity.World.Calendar.DaysPerMonth * entity.World.Calendar.HoursPerDay;
            }
            else {
                HoursToGrow = typeAttributes["hoursToGrow"].AsFloat(96);
            }
            HoursToGrow *= GenelibSystem.AnimalGrowthTime;

            if (typeAttributes.KeyExists("adultEntityCodes")) {
                string[] locations = typeAttributes["adultEntityCodes"].AsArray<string>(new string[0]);
                AdultEntityCode = new AssetLocation(locations[entity.EntityId % locations.Length]);
            }
            else if (typeAttributes.KeyExists("adultEntityCode")) {
                AdultEntityCode = new AssetLocation(typeAttributes["adultEntityCode"].AsString());
            }

            if (typeAttributes.KeyExists("initialWeight")) {
                StartingWeight = typeAttributes["initialWeight"].AsFloat();
                if (StartingWeight <= 0) {
                    throw new Exception("Error initializing aging behavior. Initial weight for " + entity.Code + " must be strictly greater than 0. Found value: " + StartingWeight);
                }
            }

            if (typeAttributes.KeyExists("finalWeight")) {
                FinalWeight = typeAttributes["finalWeight"].AsFloat();
            }
            else if (AdultEntityCode != null) {
                EntityProperties adultType = entity.World.GetEntityType(AdultEntityCode);
                if (adultType == null) {
                    entity.World.Logger.Error("Misconfigured entity. Entity with code '{0}' is configured (via genelib.age behavior) to grow into '{1}', but no such entity type was registered.", entity.Code, AdultEntityCode);
                }
                if (adultType.Attributes?.KeyExists("initialWeight") == true) {
                    FinalWeight = adultType.Attributes["initialWeight"].AsFloat();
                }
            }

            if (AdultEntityCode != null) {
                float maxVisibleGrowth = 0.9f;
                if (typeAttributes.KeyExists("maxVisibleGrowth")) {
                    maxVisibleGrowth = typeAttributes["maxVisibleGrowth"].AsFloat();
                }

                float initialScale = (float)Math.Pow(StartingWeight, 1/3f);
                float finalScale = (float)Math.Pow(FinalWeight, 1/3f);
                MaxGrowthScale = initialScale + maxVisibleGrowth * (finalScale - initialScale);
            }
            else {
                MaxGrowthScale = float.MaxValue;
            }

            float maxDailyGrowth = typeAttributes["maxDailyGrowth"].AsFloat(1.1f);
            IGameCalendar calendar = entity.Api.World.Calendar;
            float updatesPerDay = calendar.HoursPerDay * calendar.SpeedOfTime / calendar.CalendarSpeedMul / secondsPerUpdate;
            updatesPerDay *= calendar.DaysPerMonth / 30;
            maxGrowth = Math.Exp(Math.Log(maxDailyGrowth) / updatesPerDay);

            growTree = entity.WatchedAttributes.GetTreeAttribute("grow");
            if (growTree == null) {
                entity.WatchedAttributes.SetAttribute("grow", growTree = new TreeAttribute());
                double spawnAge = 0;
                string origin = entity.Attributes.GetString("origin");
                if (origin == "worldgen" || origin == "playerplaced") {
                    spawnAge = entity.World.Rand.NextSingle() * HoursToGrow * (AdultEntityCode == null ? 2 : 1);
                }
                TimeSpawned = entity.World.Calendar.TotalHours - spawnAge;
            }

            double birthDate = entity.WatchedAttributes.GetDouble("birthTotalDays", entity.World.Calendar.TotalDays);
            double spawnDate = TimeSpawned / entity.World.Calendar.HoursPerDay;
            float startAgeDays = typeAttributes["startAgeMonths"].AsFloat(0) * entity.World.Calendar.DaysPerMonth;
            if (birthDate > spawnDate - startAgeDays) {
                entity.WatchedAttributes.SetDouble("birthTotalDays", spawnDate - startAgeDays);
            }

            if (!entity.WatchedAttributes.HasAttribute("growthWeightFraction")) {
                // Set to current size, without requiring extra food
                GrowthWeightFraction = (float)ExpectedWeight((entity.World.Calendar.TotalHours - TimeSpawned) / HoursToGrow);
                entity.WatchedAttributes.SetFloat("renderScale", Math.Min(MaxGrowthScale, (float)Math.Pow(GrowthWeightFraction, 1/3f)));
            }

            callbackID = entity.World.RegisterCallback(CheckGrowth, entity.World.Rand.Next((int) secondsPerUpdate * 1000));
        }

        public void ClientUpdateScale() {
            var baseSize = entity.World.GetEntityType(entity.Code).Client.Size;
            float renderScale = entity.WatchedAttributes.GetFloat("renderScale", 1);
            entity.Properties.Client.Size = baseSize * renderScale;
        }

        protected virtual double ExpectedWeight(double ageFraction) {
            ageFraction = Math.Min(ageFraction, 1);
            double coef = 1;
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
            entity.Api.Event.EnqueueMainThreadTask( () =>
                callbackID = entity.World.RegisterCallback(CheckGrowth, (int)(secondsPerUpdate * 1000)), "register callback"
            );
        }

        protected virtual void CheckGrowth(float dt) {
            if (!entity.Alive) {
                return;
            }

            double age = entity.World.Calendar.TotalHours - TimeSpawned;
            double prevGrowth = GrowthWeightFraction;
            double expected = Math.Max(ExpectedWeight(age / HoursToGrow), prevGrowth);
            expected = Math.Min(expected, maxGrowth * prevGrowth);
            GrowthWeightFraction = (float)expected;

            AnimalHunger hunger = entity.GetBehavior<AnimalHunger>();
            if (hunger != null) {
                double prevAnimalWeight = entity.BodyCondition();
                double currentWeight = prevAnimalWeight * prevGrowth;
                double newAnimalWeight = currentWeight / expected;
                float daysPerMonth = entity.World.Calendar.DaysPerMonth;
                if (daysPerMonth < 30) {
                    newAnimalWeight = (newAnimalWeight * daysPerMonth + prevAnimalWeight * (30 - daysPerMonth)) / 30;
                }
                entity.SetBodyCondition(newAnimalWeight);
                hunger.ShiftWeight(prevAnimalWeight - newAnimalWeight);
            }
            entity.WatchedAttributes.SetFloat("renderScale", Math.Min(MaxGrowthScale, (float)Math.Pow(expected, 1/3f)));

            entity.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();

            if (age >= HoursToGrow) {
                AttemptBecomingAdult();
            }
            else {
                // Skip updating grow tree "age", which would in vanilla affect render scale depending on sizeGrowthFactor

                callbackID = entity.World.RegisterCallback(CheckGrowth, (int)(secondsPerUpdate * 1000));
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
                entity.World.Logger.Error("Misconfigured entity. Entity with code '{0}' is configured (via genelib.age behavior) to grow into '{1}', but no such entity type was registered.", entity.Code, code);
                return;
            }

            Cuboidf collisionBox = adultType.SpawnCollisionBox;

            // Delay adult spawning if we're colliding
            if (entity.World.CollisionTester.IsColliding(entity.World.BlockAccessor, collisionBox, entity.Pos.XYZ, false)) {
                callbackID = entity.World.RegisterCallback(CheckGrowth, 3000);
                return;
            }

            Entity adult = entity.World.ClassRegistry.CreateEntity(adultType);
            adult.ServerPos.SetFrom(entity.Pos);
            adult.Pos.SetFrom(entity.Pos);

            CopyAttributesTo(adult);
            adult.Attributes.SetString("origin", "growth");
            entity.World.SpawnEntity(adult);
            entity.Die(EnumDespawnReason.Expire, null);
            // Apparently entity behaviors are only set up after spawning, so we spawn first then copy
            CopyAttributesAfterSpawning(adult);
            adult.WatchedAttributes.GetTreeAttribute("grow").SetDouble("timeSpawned", adult.World.Calendar.TotalHours);
        }

        protected virtual void CopyAttributesTo(Entity adult) {
            adult.WatchedAttributes.SetInt("generation", entity.WatchedAttributes.GetInt("generation", 0));
            double birth = entity.WatchedAttributes.GetDouble("birthTotalDays", entity.World.Calendar.TotalDays);
            adult.WatchedAttributes.SetDouble("birthTotalDays", birth); // Used for antler growth
            adult.WatchedAttributes.CopyIfPresent("hunger", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("fedByPlayer", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("animalWeight", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("bodyCondition", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("ownedby", entity.WatchedAttributes);

            bool keepTexture = adult.WatchedAttributes.HasAttribute("textureIndex") 
                && entity.Properties.Client?.FirstTexture?.Alternates != null 
                && adult.Properties.Client?.FirstTexture?.Alternates != null 
                && entity.Properties.Client.FirstTexture.Alternates.Length == adult.Properties.Client.FirstTexture.Alternates.Length;

            if (keepTexture) {
                adult.WatchedAttributes.SetInt("textureIndex", entity.WatchedAttributes.GetInt("textureIndex", 0));
            }

            adult.WatchedAttributes.CopyIfPresent("nametag", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("genetics", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("motherId", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("motherName", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("motherKey", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("fatherId", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("fatherName", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("fatherKey", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("fosterId", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("fosterName", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("fosterKey", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("preventBreeding", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("neutered", entity.WatchedAttributes);

            adult.WatchedAttributes.SetLong("UID", entity.UniqueID());

            // PetAI compat
            adult.WatchedAttributes.CopyIfPresent("domesticationstatus", entity.WatchedAttributes);
        }

        protected virtual void CopyAttributesAfterSpawning(Entity adult) {
            // This no longer does anything, consider removing it
            // TODO: Consider copying over equipment
        }

        protected void UnregisterCallback() {
            if (callbackID != null) {
                entity.Api.Event.EnqueueMainThreadTask( () => {
                    entity.World.UnregisterCallback((long)callbackID);
                    callbackID = null;
                }, "unregister callback");
            }
        }

        public override void OnEntityDespawn(EntityDespawnData despawn) {
            UnregisterCallback();
        }

        public override string PropertyName() => Code;
    }
}
