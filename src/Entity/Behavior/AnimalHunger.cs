using DetailedAnimals.Extensions;
using Genelib.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace DetailedAnimals {
    public class AnimalHunger : EntityBehavior {
        public const string Code = "animalhunger";

        public float weanedAgeDays = 0;
        protected internal ITreeAttribute hungerTree;
        public Nutrient Fiber;
        public Nutrient Sugar;
        public Nutrient Starch;
        public Nutrient Fat;
        public Nutrient Protein;
        public Nutrient Water;
        public Nutrient Minerals;
        public List<Nutrient> Nutrients;
        public string[] AvoidFoodTags = new string[0];
        public string[] Specialties = new string[0];
        public float FiberDigestion = 0;
        public float MetabolicEfficiency;
        public float DaysUntilHungry = 2.5f;
        public float SaturationPerKgPerDay = 0.1f; // Based on large animals such as sheep
        public GrazeMethod[] GrazeMethods;
        public float EatRate = 1;

        protected long listenerID;
        protected int accumulator;
        protected Vec3d prevPos;
        protected AiTaskEatFromInventory eatTask;
        protected EntityBehaviorTaskAI taskAI;
#nullable enable
        protected Reproduce? reproduce;
#nullable disable

        private const int TPS = 30;
        private const int updateSeconds = 6;

        // Maximum values of each condition
        public const float STARVING = -0.9f;
        public const float FAMISHED = -0.5f;
        public const float HUNGRY = -0.2f;
        public const float PECKISH = 0.2f;
        public const float NOT_HUNGRY = 0.5f;
        public const float FULL = 0.8f;
        // No max for STUFFED

        // Approximate conversion between numbers used for player hunger and by troughs
        public const float TROUGH_SAT_PER_PLAYER_SAT = 1 / 50f;

        public double Saturation {
            get => hungerTree?.TryGetDouble("genelib.satiety") ?? hungerTree?.GetFloat("saturation") ?? 0;
            set {
                if (double.IsNaN(value)) {
                    throw new ArgumentException("Cannot set saturation value to NaN. Entity code: " + entity.Code);
                }
                hungerTree.SetDouble("genelib.satiety", value);
                hungerTree.SetFloat("saturation", Math.Max(0, (int)value));
                entity.WatchedAttributes.MarkPathDirty("hunger");
            }
        }

        public float MaxSaturation {
            get => hungerTree?.GetFloat("maxsaturation") ?? 1;
            set {
                hungerTree.SetFloat("maxsaturation", value);
                entity.WatchedAttributes.MarkPathDirty("hunger");
            }
        }

        public double AdjustedMaxSaturation {
            get => MaxSaturation 
                    * Math.Max(0.1f, entity.WatchedAttributes.GetFloat("growthWeightFraction", 1))
                    * (entity.BodyCondition() + 1) / 2;
        }

        public double Fullness {
            get => Saturation / AdjustedMaxSaturation;
        }

        public double LastUpdateHours {
            get => hungerTree.GetDouble("lastupdateHours");
            set {
                hungerTree.SetDouble("lastupdateHours", value);
                entity.WatchedAttributes.MarkPathDirty("hunger");
            }
        }

        public AnimalHunger(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes) {
            if (typeAttributes.KeyExists("daysUntilHungry")) {
                DaysUntilHungry = typeAttributes["daysUntilHungry"].AsFloat();
            }
            if (typeAttributes.KeyExists("saturationPerKgPerDay")) {
                SaturationPerKgPerDay = typeAttributes["saturationPerKgPerDay"].AsFloat() * TROUGH_SAT_PER_PLAYER_SAT;
            }

            if (typeAttributes.KeyExists("monthsUntilWeaned")) {
                weanedAgeDays = typeAttributes["monthsUntilWeaned"].AsFloat() * entity.World.Calendar.DaysPerMonth;
            }
            if (typeAttributes.KeyExists("avoidFoodTags")) {
                AvoidFoodTags = typeAttributes["avoidFoodTags"].AsArray<string>();
            }
            if (typeAttributes.KeyExists("specialties")) {
                Specialties = typeAttributes["specialties"].AsArray<string>();
            }
            if (typeAttributes.KeyExists("fiberDigestion")) {
                FiberDigestion = typeAttributes["fiberDigestion"].AsFloat();
            }
            if (typeAttributes.KeyExists("grazeMethods")) {
                GrazeMethods = typeAttributes["grazeMethods"].AsArray<GrazeMethod>();
            }
            if (typeAttributes.KeyExists("eatRate")) {
                EatRate = typeAttributes["eatRate"].AsFloat();
            }

            Fiber = new Nutrient("fiber", typeAttributes, this);
            Sugar = new Nutrient("sugar", typeAttributes, this);
            Starch = new Nutrient("starch", typeAttributes, this);
            Fat = new Nutrient("fat", typeAttributes, this);
            Protein = new Nutrient("protein", typeAttributes, this);
            Water = new Nutrient("water", typeAttributes, this);
            Minerals = new Nutrient("minerals", typeAttributes, this);
            Nutrients = new List<Nutrient> { Fiber, Sugar, Starch, Fat, Protein, Water, Minerals };

            if (entity.Api.Side == EnumAppSide.Client) {
                return;
            }

            JsonObject eatTaskConfig = typeAttributes["eatFromInventoryTask"];
            if (eatTaskConfig != null) {
                eatTask = new AiTaskEatFromInventory((EntityAgent)entity, eatTaskConfig, null, this);
            }

            // Do NOT create hunger tree on the client side, or it won't sync over the values
            hungerTree = entity.WatchedAttributes.GetOrAddTreeAttribute("hunger");
            MaxSaturation = entity.HealthyAdultWeightKg() * SaturationPerKgPerDay * DaysUntilHungry / 2;

            accumulator = entity.World.Rand.Next(updateSeconds * TPS);
            prevPos = entity.Pos.XYZ;
            entity.Api.Event.EnqueueMainThreadTask( () =>
                listenerID = entity.World.RegisterGameTickListener(SlowServerTick, 12000), "register tick listener"
            );
            ApplyNutritionEffects();
        }

        public override void AfterInitialized(bool onFirstSpawn) {
            if (onFirstSpawn) {
                LastUpdateHours = entity.World.Calendar.TotalHours;
                // TODO: Set chicks to start full after hatching
                if (false) {
                    Saturation = AdjustedMaxSaturation * FULL;
                }
            }

            if (entity.Api.Side != EnumAppSide.Server) {
                return;
            }
            taskAI = entity.GetBehavior<EntityBehaviorTaskAI>();
            if (taskAI == null) {
                throw new FormatException(entity.Code + " has no task AI behavior needed by " + Code);
            }

            reproduce = entity.GetBehavior<Reproduce>();
        }

        public override void OnEntityDespawn(EntityDespawnData despawn) {
            base.OnEntityDespawn(despawn);
            entity.Api.Event.EnqueueMainThreadTask( () =>
                entity.World.UnregisterGameTickListener(listenerID), "unregister tick listener"
            );
        }

        public bool EatsGrassOrRoots() {
            return GrazeMethods != null && GrazeMethods.Length > 0;
        }

        public GrazeMethod GetGrazeMethod(Random random) {
            return GrazeMethods[random.Next(GrazeMethods.Length)];
        }

        public virtual void ApplyNutritionEffects() {
            float fiber = Math.Max(0, Fiber.Value);
            float sugar = Math.Max(0, Sugar.Value);
            float starch = Math.Max(0, Starch.Value);
            float fat = Math.Max(0, Fat.Value);
            float protein = Math.Max(0, Protein.Value);
            float water = Math.Max(0, Water.Value);
            float minerals = Math.Max(0, Minerals.Value);

            float metabolic_efficiency = 0.1f * starch + 0.2f * water + 0.1f * fiber * FiberDigestion;
            float health = 0.1f * fiber + 0.1f * minerals;
            float strength = 0.1f * protein + 0.05f * fat;
            float speed = 0.1f * sugar;
            float stamina = 0.05f * starch + 0.1f * minerals + 0.15f * water;
            float milkegg = 0.18f * fat + 0.06f * protein + 0.06f * minerals;
            float wool = 0.1f * protein + 0.05f * fiber;
            float growth = 0.08f * fat + 0.12f * protein;
            float attack_power = 0.1f * protein;
            float meat_drops = 0.1f * protein;
            float fat_drops = 0.1f * fat;

            if (float.IsNaN(metabolic_efficiency)) {
                throw new ArgumentException("Cannot set metabolic efficiency to NaN. Entity code: " + entity.Code);
            }
            MetabolicEfficiency = metabolic_efficiency;

            EntityBehaviorHealth healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();
            if (healthBehavior == null) {
                DetailedAnimalsModSystem.ServerAPI.Logger.Warning(Code + " expected non-null health behavior for entity " + entity.Code);
            }
            else {
                healthBehavior.SetMaxHealthModifiers("nutrientHealthMod", health);
                healthBehavior.MarkDirty();
            }

            // TODO: give other bonuses

            // Consider adding later:
            // Fiber: disease resistance
            // Sugar: jump height, energy
            // Fat: defense, fertility
            // Minerals: fertility
        }

        private double getAgeDays() {
            return entity.World.Calendar.TotalDays - entity.WatchedAttributes.GetFloat("birthTotalDays", -99999);
        }

        // Returns null if this is the sort of food the animal wants right now, otherwise returns a string depending on
        // the reason the food is not wanted
        public string AvoidReason(NutritionData data, double satiety) {
            if (Fullness < FAMISHED) {
                return null;
            }
            if (data != null) {
                satiety *= 1 - data.Values["fiber"] * (1 - FiberDigestion);
            }
            satiety = Math.Min(satiety, AdjustedMaxSaturation - Saturation);
            satiety /= AdjustedMaxSaturation;
            if (data == null) {
                return null;
            }
            double sum = 0;
            double worst = 0;
            string worstName = null;
            foreach (Nutrient nutrient in Nutrients) {
                double gain = nutrient.ValueIfAdded(satiety * data.Values[nutrient.Name]) - nutrient.Value;
                if (gain > 0 || Fullness > PECKISH || data.Values[nutrient.Name] > nutrient.Usage ) {
                    sum += gain;
                    if (gain < worst) {
                        worst = gain;
                        worstName = nutrient.Name;
                    }
                }
            }
            if (sum < 0 && 2 * sum < Fullness) {
                return Fullness < PECKISH ? worstName : "food";
            }
            return null;
        }

        public bool CanEat(ItemStack itemstack, FoodNutritionProperties nutriProps, NutritionData data) {
            if (entity.BodyCondition() > 1.8) {
                return false;
            }
            double fullness = Fullness;
            if (fullness > FULL) {
                return false;
            }


            if (nutriProps != null) {
                if (nutriProps.Health < 0) {
                    return false;
                }

                if (nutriProps.Intoxication > 0) {
                    return false;
                }
            }

            // Respect "skipFoodTags" and "specialties" even if animal is starving
            string[] foodTags = itemstack.Collectible.Attributes?["foodTags"].AsArray<string>() ?? new string[0];
            CreatureDiet diet = entity.Properties.Attributes["creatureDiet"].AsObject<CreatureDiet>();
            if (diet.SkipFoodTags != null) {
                foreach (string skipTag in diet.SkipFoodTags) {
                    if (foodTags.Contains(skipTag)) {
                        return false;
                    }
                }
            }

            if (data?.Specialties != null) {
                if (Specialties == null) {
                        return false;
                }
                foreach (string specialty in data.Specialties) {
                    if (specialty == "lactose" && CanDigestMilk()) {
                        continue;
                    }
                    if (!Specialties.Contains(specialty)) {
                        return false;
                    }
                }
            }

            if (!diet.Matches(itemstack)) {
                if (foodTags.Length == 0 && nutriProps == null && data == null && (itemstack.Collectible?.Attributes?.KeyExists("satiety")  != true)) {
                    return false;
                }
                return WantsEmergencyFood();
            }
            foreach (string avoid in AvoidFoodTags) {
                if (foodTags.Contains(avoid)) {
                    return false;
                }
            }
            return true;
        }

        public bool CanEat(ItemStack itemstack) {
            FoodNutritionProperties nutriProps = itemstack.Collectible.GetNutritionProperties(entity.World, itemstack, entity);
            NutritionData data = GetNutritionData(itemstack, nutriProps);
            return CanEat(itemstack, nutriProps, data);
        }

        public bool WantsFood(ItemStack itemstack) {
            FoodNutritionProperties nutriProps = itemstack.Collectible.GetNutritionProperties(entity.World, itemstack, entity);
            NutritionData data = GetNutritionData(itemstack, nutriProps);
            if (!CanEat(itemstack, nutriProps, data)) {
                return false;
            }
            return AvoidReason(data, GetBaseSatiety(itemstack, nutriProps)) == null;
        }

        public bool WantsEmergencyFood() {
            double fullness = Fullness;
            return fullness < STARVING
                || (entity.BodyCondition() < 0.7 && fullness < HUNGRY) 
                || (entity.BodyCondition() < 0.85 && fullness < FAMISHED);
        }

        public bool CanDigestMilk() {
            return getAgeDays() < 1.5 * weanedAgeDays;
        }

        public bool StartedWeaning() {
            return getAgeDays() > 0.25 * weanedAgeDays;
        }

        public bool WantsMilk() {
            return getAgeDays() < (0.25 + entity.World.Rand.NextSingle()) * weanedAgeDays;
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled) {
            if (entity.Api.Side == EnumAppSide.Client) {
                return;
            }
            handled = EnumHandling.PassThrough;
            if (slot.Empty) {
                return;
            }
            if (!entity.Alive) {
                return;
            }
            ItemStack itemstack = slot.Itemstack;
            FoodNutritionProperties nutriProps = itemstack.Collectible.GetNutritionProperties(entity.World, itemstack, entity);
            NutritionData data = GetNutritionData(itemstack, nutriProps);
            if (!CanEat(itemstack, nutriProps, data)) {
                return;
            }
            string avoidReason = AvoidReason(data, GetBaseSatiety(itemstack, nutriProps));
            if (avoidReason != null) {
                messagePlayer("detailedanimals:message-wrongnutrient-" + avoidReason, byEntity);
                return;
            }

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            entity.World.Api.Logger.Audit(byPlayer?.PlayerName + " fed " + itemstack.Collectible?.Code + " to " + entity.Code + " ID " + entity.EntityId + " at " + entity.Pos.XYZ.AsBlockPos);

            bool consumeItem = true;
            EntityBehavior tameable = entity.GetBehavior("tameable");
            if (tameable != null) {
                int id = slot.Itemstack.Id;
                int count = slot.Itemstack.StackSize;
                tameable.OnInteract(byEntity, slot, hitPosition, mode, ref handled);
                consumeItem = id == slot.Itemstack.Id && count == slot.Itemstack.StackSize;
            }
            handled = EnumHandling.PreventSubsequent;
            if (!entity.Alive) {
                // Check again, as taming interaction may kill the entity and spawn a new, tame, one
                return;
            }
            Eat(slot, byEntity, false, data, nutriProps, consumeItem);
        }

        public NutritionData GetNutritionData(ItemStack itemstack, FoodNutritionProperties nutriProps) {
            string[] foodTags = itemstack.Collectible.Attributes?["foodTags"].AsArray<string>() ?? new string[0];
            return GetNutritionData(itemstack, nutriProps, foodTags);
        }

        public NutritionData GetNutritionData(ItemStack itemstack, FoodNutritionProperties nutriProps, string[] foodTags) {
            NutritionData data = null;
            foreach (string tag in foodTags) {
                NutritionData tagData = NutritionData.Get(tag);
                if (data == null || (tagData != null && tagData.Priority > data.Priority)) {
                    data = tagData;
                }
            }
            if (data == null && nutriProps != null) {
                data = nutriProps.FoodCategory switch {
                    EnumFoodCategory.Fruit => NutritionData.Get("fruit"),
                    EnumFoodCategory.Grain => NutritionData.Get("grain"),
                    EnumFoodCategory.Protein => NutritionData.Get("meat"),
                    EnumFoodCategory.Dairy => NutritionData.Get("cheese"),
                    EnumFoodCategory.Vegetable => NutritionData.Get("vegetable"),
                    _ => null,
                };
            }
            return data;
        }

        public static float GetBaseSatiety(ItemStack itemstack, FoodNutritionProperties nutriProps) {
            float satiety = 50;
            if (itemstack.Collectible.Attributes?.KeyExists("satiety") == true) {
                satiety = itemstack.Collectible.Attributes["satiety"].AsFloat();
            }
            else if (nutriProps != null) {
                satiety = nutriProps.Satiety;
            }
            return satiety * TROUGH_SAT_PER_PLAYER_SAT;
        }

        public void TryEatFromInventory() {
            if (eatTask == null) {
                return;
            }
            if ((taskAI.TaskManager.ActiveTasksBySlot[eatTask.Slot] == null || taskAI.TaskManager.ActiveTasksBySlot[eatTask.Slot].Priority < eatTask.Priority) && eatTask.ShouldExecute()) {
                taskAI.TaskManager.ExecuteTask(eatTask, eatTask.Slot);
            }
        }

        public void Eat(ItemStack itemstack, bool fedByPlayer) {
            Eat(new DummySlot(itemstack), fedByPlayer);
        }

        public void Eat(ItemSlot slot, bool fedByPlayer) {
            ItemStack itemstack = slot.Itemstack;
            FoodNutritionProperties nutriProps = itemstack.Collectible.GetNutritionProperties(entity.World, itemstack, entity);
            string[] foodTags = itemstack.Collectible.Attributes?["foodTags"].AsArray<string>() ?? new string[0];
            Eat(slot, null, fedByPlayer, GetNutritionData(itemstack, nutriProps, foodTags), nutriProps);
        }

        public void Eat(ItemSlot slot, Entity fedBy, bool fedByPlayer, NutritionData data, FoodNutritionProperties nutriProps, bool consumeItem=true) {
            if (entity.World.Side != EnumAppSide.Server) {
                return;
            }
            // Game code does it like this, assuming because we can't trust that fedBy.Player will be synchronized
            IPlayer player = fedBy?.World.PlayerByUid((fedBy as EntityPlayer)?.PlayerUID);
            entity.PlayEntitySound("eat", player);
            if (player != null) {
                entity.WatchedAttributes.SetBool("fedByPlayer", true);
            }

            EntityAgent agent = entity as EntityAgent;
            ItemStack itemstack = slot.Itemstack;

            if (consumeItem && itemstack.StackSize <= 0) {
                return;
            }


            // Based on Collectible.tryEatStop
            TransitionState state = itemstack.Collectible.UpdateAndGetTransitionState(entity.World, slot, EnumTransitionType.Perish);
            float spoilState = state != null ? state.TransitionLevel : 0;
            float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, agent);
            if (nutriProps != null) {
                float healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, agent);
                float intox = entity.WatchedAttributes.GetFloat("intoxication");
                entity.WatchedAttributes.SetFloat("intoxication", Math.Min(1.1f, intox + nutriProps.Intoxication));
                float healthChange = nutriProps.Health * healthLossMul;
                if (healthChange != 0) {
                    entity.ReceiveDamage(new DamageSource() {
                            Source = EnumDamageSource.Internal, 
                            Type = healthChange > 0 ? EnumDamageType.Heal : EnumDamageType.Poison 
                        }, Math.Abs(healthChange));
                }
            }


            float satiety = GetBaseSatiety(itemstack, nutriProps);
            satiety *= satLossMul;
            Eat(data, satiety, fedByPlayer || player != null);

            if (consumeItem) {
                slot.TakeOut(1);
                if (nutriProps?.EatenStack != null) {
                    if (slot.Empty) {
                        slot.Itemstack = nutriProps.EatenStack.ResolvedItemstack.Clone();
                    }
                    else {
                        if (player == null || !player.InventoryManager.TryGiveItemstack(nutriProps.EatenStack.ResolvedItemstack.Clone(), true)) {
                            entity.World.SpawnItemEntity(nutriProps.EatenStack.ResolvedItemstack.Clone(), fedBy.Pos.XYZ);
                        }
                    }
                }
                slot.MarkDirty();
                player?.InventoryManager.BroadcastHotbarSlot();
            }
        }

        public void Eat(NutritionData data, double satiety, bool fedByPlayer) {
            if (data != null) {
                satiety *= 1 - data.Values["fiber"] * (1 - FiberDigestion);
            }

            double prevSaturation = Saturation;
            EntityAgent agent = entity as EntityAgent;
            agent?.ReceiveSaturation((float)satiety, data?.FoodCategory ?? EnumFoodCategory.NoNutrition);
            double maxsat = AdjustedMaxSaturation;
            Saturation = Math.Clamp(prevSaturation + satiety, -maxsat, maxsat);
            double gain = (Saturation - prevSaturation) / maxsat;
            foreach (Nutrient nutrient in Nutrients) {
                nutrient.Gain(gain * (data?.Values[nutrient.Name] ?? 0));
            }

            if (fedByPlayer) {
                entity.WatchedAttributes.SetBool("fedByPlayer", true);
                entity.WatchedAttributes.SetDouble("fedByPlayerTotalSatiety", gain + entity.WatchedAttributes.GetDouble("fedByPlayerTotalSatiety", 0));
            }

            ApplyNutritionEffects();
        }

        private void SlowServerTick(float dt)
        {
            // Same temperature-hunger logic as EntityBehaviorHunger uses
            bool harshWinters = entity.World.Config.GetString("harshWinters").ToBool(true);

            float temperature = entity.World.BlockAccessor.GetClimateAt(entity.Pos.AsBlockPos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, entity.World.Calendar.TotalDays).Temperature;
            if (temperature >= 2 || !harshWinters)
            {
                entity.Stats.Remove("hungerrate", "resistcold");
            }
            else
            {
                // 0..1
                float diff = GameMath.Clamp(2 - temperature, 0, 10);

                Room room = entity.World.Api.ModLoader.GetModSystem<RoomRegistry>().GetRoomForPosition(entity.Pos.AsBlockPos);

                entity.Stats.Set("hungerrate", "resistcold", room.ExitCount == 0 ? 0 : diff / 40f, true);
            }
        }

        public override void OnGameTick(float deltaTime) {
            // Don't put in SlowServerTick, because that still gets called when game is paused
            // And don't reference game calendar, because entities in unloaded chunks have no way to eat and so should 
            // not get hungry.

            if (entity.Api.Side == EnumAppSide.Client) {
                return;
            }
            ++accumulator;
            if (accumulator > updateSeconds * TPS) {
                accumulator = 0;
                double currentHours = entity.World.Calendar.TotalHours;
                float updateRateHours = updateSeconds / 120f;
                double lastUpdateHours = LastUpdateHours;
                double updates = (currentHours - lastUpdateHours) / updateRateHours;
                if (updates <= 0) {
                    return;
                }

                float intoxication = entity.WatchedAttributes.GetFloat("intoxication");
                if (intoxication > 0)
                {
                    entity.WatchedAttributes.SetFloat("intoxication", Math.Max(0, intoxication - 0.005f * (float)updates));
                }

                float updatesPerDay = 48 * 60 / updateSeconds;
                double baseHungerRate = AdjustedMaxSaturation * 2 / updatesPerDay / DaysUntilHungry;

                Vec3d currentPos = entity.Pos.XYZ;
                double distance = currentPos.DistanceTo(prevPos) / updates;
                distance = Math.Max(0, distance + currentPos.Y - prevPos.Y); // Climbing/falling adjustment
                // Riding a boat or train shouldn't make the animal hungrier
                if (entity.WatchedAttributes["mountedOn"] != null) {
                    distance = 0;
                }
                prevPos = currentPos;
                double metersPerSecond = distance / updateSeconds;
                double work = 0.95f + 0.3f * Math.Min(metersPerSecond, 10) / 10;

                // Do not reference Calendar.SpeedOfTime, because that increases while sleeping
                float timespeed = entity.Api.World.Calendar.CalendarSpeedMul * 2;
                float hungerrate = entity.Stats.GetBlended("hungerrate");
                double saturationConsumed = baseHungerRate * work * hungerrate * timespeed / (1 + MetabolicEfficiency);
                ConsumeSaturation(saturationConsumed);
                // When player sleeps or chunk is unloaded, regain weight but don't starve
                while ((currentHours - lastUpdateHours > 2 * updateRateHours) && (Fullness > -0.8f)) {
                    UpdateCondition(updateRateHours);
                    ConsumeSaturation(saturationConsumed / work);
                    lastUpdateHours += updateRateHours;
                }
                UpdateCondition(updateRateHours);
                LastUpdateHours = currentHours;
                ApplyNutritionEffects();

                TryEatFromInventory();
            }
        }

        public double WeightShiftAmount(double fullness) {
            double gain = fullness * fullness * fullness;
            double recovery = 1 - entity.BodyCondition();
            return (gain + recovery) / 2;
        }

        public double WeightShiftAmount() {
            return WeightShiftAmount(Fullness);
        }

        // Become fatter or thinner
        public void UpdateCondition(float hours) {
            double extraGrowthTarget = reproduce?.ExtraGrowthTarget ?? 0;
            double extraGrowthTargetHour = reproduce?.ExtraGrowthTargetHour ?? 0;

            double numUpdatesUntilTarget = Math.Max(1, (extraGrowthTargetHour - entity.World.Calendar.TotalHours) * 120 / updateSeconds);
            double extraGrowthNeeded = (extraGrowthTarget - entity.ExtraGrowth()) / numUpdatesUntilTarget;
            double yearSpeed = 30 / entity.World.Calendar.DaysPerMonth;
            extraGrowthNeeded = Math.Clamp(extraGrowthNeeded, -0.00003 * updateSeconds * yearSpeed, 0.00003 * updateSeconds * yearSpeed);

            if (extraGrowthNeeded < 0 && entity.BodyCondition() < DetailedHarvestable.LEAN) {
                entity.SetBodyCondition(entity.BodyCondition() - extraGrowthNeeded);
                entity.SetExtraGrowth(entity.ExtraGrowth() + extraGrowthNeeded);
            }
            else if (extraGrowthNeeded > 0) {
                double satNeededForGrowth = SatietyForWeightChange(extraGrowthNeeded / yearSpeed);

                double prevSaturation = Saturation;
                double ceiling = Math.Max(0.8 * AdjustedMaxSaturation, prevSaturation);
                double floor = Math.Min(-0.8 * AdjustedMaxSaturation, prevSaturation);
                double nextSaturation = Math.Clamp(prevSaturation - satNeededForGrowth, floor, ceiling);
                double satForGrowth = prevSaturation - nextSaturation; // Negate once now and again when consumed
                if (satForGrowth != 0) {
                    ConsumeSaturation(satForGrowth);
                    entity.SetExtraGrowth(entity.ExtraGrowth() + extraGrowthNeeded * satForGrowth / satNeededForGrowth);
                }
            }

            double weightShiftRate = 0.5 * hours / 24 / 12.5;
            ShiftWeight(weightShiftRate * WeightShiftAmount());
        }

        // In: Change in weight as a fraction of base weight
        // Out: Resulting cost in satiety/saturation
        public double SatietyForWeightChange(double deltaWeight) {
            double inefficiency = deltaWeight > 0 ? 1.05 : 0.95;
            double fractionOfOwnWeightEatenPerDay = 0.04;
            double totalSaturation = AdjustedMaxSaturation * 2;
            return deltaWeight * inefficiency / fractionOfOwnWeightEatenPerDay * totalSaturation / DaysUntilHungry;
        }

        public void ShiftWeight(double deltaWeight) {
            double deltaSat = SatietyForWeightChange(deltaWeight);
            entity.SetBodyCondition(Math.Clamp(entity.BodyCondition() + deltaWeight, 0.85, 2.0)); // TODO: After making wild animals able to find food, replace 0.85 with 0.5
            ConsumeSaturation(deltaSat);
            if (deltaWeight > 0) {
                Fat.Consume(deltaSat / 4);
                Protein.Consume(deltaSat / 4);
            }
        }

        public void ConsumeSaturation(double amount) {
            double prevSaturation = Saturation;
            Saturation = Math.Clamp(prevSaturation - amount, -AdjustedMaxSaturation, AdjustedMaxSaturation);
            double loss = (prevSaturation - Saturation) / AdjustedMaxSaturation;
            if (loss > 0) {
                foreach (Nutrient nutrient in Nutrients) {
                    nutrient.Consume(loss);
                }
            }
            ApplyNutritionEffects();
        }

        private void messagePlayer(String langKey, Entity byEntity) {
            String message = Lang.GetUnformatted(langKey).Replace("{entity}", entity.GetDisplayName());
            ((byEntity as EntityPlayer)?.Player as IServerPlayer)?.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
        }

        public override void GetInfoText(StringBuilder infotext) {
            if (!entity.Alive) {
                return;
            }
            base.GetInfoText(infotext);
            hungerTree = entity.WatchedAttributes.GetTreeAttribute("hunger");
            if (hungerTree == null) {
                return;
            }
            double[] hungerBoundaries = new double[] { FULL, NOT_HUNGRY, PECKISH, HUNGRY, FAMISHED, STARVING };
            int hungerScore = 0;
            double fullness = Fullness;
            foreach (double b in hungerBoundaries) {
                if (fullness < b) {
                    hungerScore += 1;
                }
                else {
                    break;
                }
            }

            string suffix = entity.IsMale() ? "-male" : "-female";
            string text = VSExtensions.GetLangOptionallySuffixed("detailedanimals:infotext-hunger" + hungerScore.ToString(), suffix);
            if (hungerScore == 0 || hungerScore >= 5) {
                infotext.AppendLine($"<font color=\"#cc2211\">{text}</font>");
            }
            else {
                infotext.AppendLine(text);
            }
        }

        public override string PropertyName() => Code;
    }
}
