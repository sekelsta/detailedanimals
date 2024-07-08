using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

namespace Genelib {
    public class AnimalHunger : EntityBehavior {
        public const string Code = GeneticsModSystem.NamePrefix + "hunger";

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
        public float DaysUntilHungry = 4;

        protected long listenerID;
        protected int accumulator;
        protected Vec3d prevPos;

        private const int TPS = 30;
        private const int updateSeconds = 12;

        // Maximum values of each condition
        private const float STARVING = -0.9f;
        private const float FAMISHED = -0.6f;
        private const float VERY_HUNGRY = -0.3f;
        private const float HUNGRY = 0.1f;
        private const float PECKISH = 0.4f;
        private const float NOT_HUNGRY = 0.6f;
        private const float FULL = 0.8f;
        // No max for STUFFED

        public float Saturation {
            get => hungerTree.GetFloat("saturation");
            set {
                if (float.IsNaN(value)) {
                    throw new ArgumentException("Cannot set saturation value to NaN");
                }
                hungerTree.SetFloat("saturation", value);
                entity.WatchedAttributes.MarkPathDirty("hunger");
            }
        }

        public float MaxSaturation {
            get => hungerTree.GetFloat("maxsaturation");
            set {
                hungerTree.SetFloat("maxsaturation", value);
                entity.WatchedAttributes.MarkPathDirty("hunger");
            }
        }

        public float AdjustedMaxSaturation {
            get => MaxSaturation * Math.Max(0.1f, entity.WatchedAttributes.GetFloat("growthWeightFraction", 1));
        }

        public float Fullness {
            get => Saturation / AdjustedMaxSaturation;
        }

        public float AnimalWeight {
            get => entity.WatchedAttributes.GetFloat("animalWeight", 1f);
            set {
                if (float.IsNaN(value)) {
                    throw new ArgumentException("Cannot set animalWeight to NaN");
                }
                entity.WatchedAttributes.SetFloat("animalWeight", value);
            }
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
            hungerTree = entity.WatchedAttributes.GetOrAddTreeAttribute("hunger");
            if (entity.Api.Side == EnumAppSide.Client) {
                return;
            }

            accumulator = entity.World.Rand.Next(updateSeconds * TPS);
            if (typeAttributes.KeyExists("maxsaturation")) {
                MaxSaturation = typeAttributes["maxsaturation"].AsFloat();
            }
            else {
                float adultWeightKg = entity.Properties?.Attributes?["adultWeightKg"]?.AsFloat(160) ?? 160;
                MaxSaturation = adultWeightKg / 20;
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
            prevPos = entity.ServerPos.XYZ;

            Fiber = new Nutrient("fiber", typeAttributes, this);
            Sugar = new Nutrient("sugar", typeAttributes, this);
            Starch = new Nutrient("starch", typeAttributes, this);
            Fat = new Nutrient("fat", typeAttributes, this);
            Protein = new Nutrient("protein", typeAttributes, this);
            Water = new Nutrient("water", typeAttributes, this);
            Minerals = new Nutrient("minerals", typeAttributes, this);
            Nutrients = new List<Nutrient> { Fiber, Sugar, Starch, Fat, Protein, Water, Minerals };
            listenerID = entity.World.RegisterGameTickListener(SlowServerTick, 12000);
            ApplyNutritionEffects();
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);
            entity.World.UnregisterGameTickListener(listenerID);
        }

        public virtual void ApplyNutritionEffects() {
            float fiber = Fiber.Value;
            float sugar = Sugar.Value;
            float starch = Starch.Value;
            float fat = Fat.Value;
            float protein = Protein.Value;
            float water = Water.Value;
            float minerals = Minerals.Value;

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
                throw new ArgumentException("Cannot set metabolic efficiency to NaN");
            }
            MetabolicEfficiency = metabolic_efficiency;

            EntityBehaviorHealth healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();
            if (healthBehavior == null) {
                GeneticsModSystem.ServerAPI.Logger.Warning(Code + " expected non-null health behavior for entity " + entity.Code);
            }
            else {
                healthBehavior.MaxHealthModifiers["nutrientHealthMod"] = health;
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
        public string AvoidReason(NutritionData data, float satiety) {
            if (Fullness < FAMISHED) {
                return null;
            }
            satiety = Math.Min(satiety, AdjustedMaxSaturation - Saturation);
            satiety /= AdjustedMaxSaturation;
            if (data == null) {
                return null;
            }
            foreach (Nutrient nutrient in Nutrients) {
                if (nutrient.Name.Equals("sugar")) {
                    continue;
                }
                float newLevel = nutrient.Level + satiety * data.Values[nutrient.Name];
                if (newLevel > nutrient.MaxSafe) {
                    return nutrient.Name;
                }
            }
            return null;
        }

        public bool WantsEmergencyFood() {
            float fullness = Fullness;
            return fullness < STARVING
                || (AnimalWeight < 0.7 && fullness < HUNGRY) 
                || (AnimalWeight < 0.85 && fullness < VERY_HUNGRY);
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled) {
            if (entity.Api.Side == EnumAppSide.Client) {
                return;
            }
            if (slot.Empty) {
                handled = EnumHandling.PassThrough;
                return;
            }

            if (AnimalWeight > 1.8f) {
                handled = EnumHandling.PassThrough;
                return;
            }
            float fullness = Fullness;
            if (fullness > FULL) {
                handled = EnumHandling.PassThrough;
                return;
            }

            // Respect "skipFoodTags" and "specialties" even if animal is starving
            ItemStack itemstack = slot.Itemstack;
            string[] foodTags = itemstack.Collectible.Attributes?["foodTags"].AsArray<string>() ?? new string[0];
            CreatureDiet diet = entity.Properties.Attributes["creatureDiet"].AsObject<CreatureDiet>();
            if (diet.SkipFoodTags != null) {
                foreach (string skipTag in diet.SkipFoodTags) {
                    if (foodTags.Contains(skipTag)) {
                        handled = EnumHandling.PassThrough;
                        return;
                    }
                }
            }

            FoodNutritionProperties nutriProps = itemstack.Collectible.GetNutritionProperties(entity.World, itemstack, entity);
            NutritionData data = GetNutritionData(itemstack, nutriProps, foodTags);

            if (data?.Specialties != null) {
                if (Specialties == null) {
                        handled = EnumHandling.PassThrough;
                        return;
                }
                foreach (string specialty in data.Specialties) {
                    if (specialty == "lactose" && getAgeDays() < 1.5 * weanedAgeDays) {
                        continue;
                    }
                    if (!Specialties.Contains(specialty)) {
                        handled = EnumHandling.PassThrough;
                        return;
                    }
                }
            }

            if (!diet.Matches(itemstack)) {
                if (WantsEmergencyFood()) {
                    Eat(slot, byEntity, data, nutriProps);
                    handled = EnumHandling.PreventSubsequent;
                    return;
                }
                handled = EnumHandling.PassThrough;
                return;
            }
            foreach (string avoid in AvoidFoodTags) {
                if (foodTags.Contains(avoid)) {
                    handled = EnumHandling.PassThrough;
                    return;
                }
            }
            string avoidReason = AvoidReason(data, GetBaseSatiety(nutriProps, itemstack));
            if (avoidReason != null) {
                messagePlayer("genelib:message-wrongnutrient-" + avoidReason, byEntity);
                handled = EnumHandling.PassThrough;
                return;
            }
            Eat(slot, byEntity, data, nutriProps);
            handled = EnumHandling.PreventSubsequent;
            return;
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

        public static float GetBaseSatiety(FoodNutritionProperties nutriProps, ItemStack itemstack) {
            float satiety = 50;
            if (itemstack.Collectible.Attributes?.KeyExists("satiety") == true) {
                satiety = itemstack.Collectible.Attributes["satiety"].AsFloat();
            }
            else if (nutriProps != null) {
                satiety = nutriProps.Satiety;
            }
            return satiety / 100;  // Approximate conversion between numbers used for player hunger and by troughs
        }

        public void Eat(ItemStack itemstack) {
            Eat(new DummySlot(itemstack));
        }

        public void Eat(ItemSlot slot) {
            ItemStack itemstack = slot.Itemstack;
            FoodNutritionProperties nutriProps = itemstack.Collectible.GetNutritionProperties(entity.World, itemstack, entity);
            string[] foodTags = itemstack.Collectible.Attributes?["foodTags"].AsArray<string>() ?? new string[0];
            Eat(slot, null, GetNutritionData(itemstack, nutriProps, foodTags), nutriProps);
        }

        public void Eat(ItemSlot slot, Entity fedBy, NutritionData data, FoodNutritionProperties nutriProps) {
            if (entity.World.Side != EnumAppSide.Server) {
                return;
            }
            // Game code does it like this, assuming because we can't trust that fedBy.Player will be synchronized
            IPlayer player = fedBy?.World.PlayerByUid((fedBy as EntityPlayer)?.PlayerUID);
            entity.PlayEntitySound("eat", player);

            EntityAgent agent = entity as EntityAgent;
            ItemStack itemstack = slot.Itemstack;


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


            float satiety = GetBaseSatiety(nutriProps, itemstack);
            satiety *= satLossMul;
            if (data != null) {
                satiety *= 1 - data.Values["fiber"] * (1 - FiberDigestion);
            }
            float prevSaturation = Saturation;
            agent?.ReceiveSaturation(satiety, data?.FoodCategory ?? EnumFoodCategory.NoNutrition);
            float maxsat = AdjustedMaxSaturation;
            Saturation = Math.Clamp(prevSaturation + satiety, -maxsat, maxsat);
            float gain = (Saturation - prevSaturation) / maxsat;
            foreach (Nutrient nutrient in Nutrients) {
                nutrient.Gain(gain * (data?.Values[nutrient.Name] ?? 0));
            }

            // Make sure itemstack doesn't get modified twice
            bool alreadyUsed = false;
            // TODO: Use reflection to check if PetAI:EntityBehaviorTameable exists and call its onInteract
            // If so, set alreadyUsed to true if the stack has changed
            if (!alreadyUsed) {
                slot.TakeOut(1);
                if (nutriProps?.EatenStack != null) {
                    if (slot.Empty) {
                        slot.Itemstack = nutriProps.EatenStack.ResolvedItemstack.Clone();
                    }
                    else {
                        if (player == null || !player.InventoryManager.TryGiveItemstack(nutriProps.EatenStack.ResolvedItemstack.Clone(), true)) {
                            entity.World.SpawnItemEntity(nutriProps.EatenStack.ResolvedItemstack.Clone(), fedBy.SidedPos.XYZ);
                        }
                    }
                }
                slot.MarkDirty();
                player?.InventoryManager.BroadcastHotbarSlot();
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
                float updateRateHours = 0.1f;
                double lastUpdateHours = LastUpdateHours;
                double updates = (currentHours - lastUpdateHours) / updateRateHours;

                float intoxication = entity.WatchedAttributes.GetFloat("intoxication");
                if (intoxication > 0)
                {
                    entity.WatchedAttributes.SetFloat("intoxication", Math.Max(0, intoxication - 0.005f * (float)updates));
                }

                float updatesPerDay = 48 * 60 / updateSeconds;
                float baseHungerRate = AdjustedMaxSaturation * 2 / updatesPerDay / DaysUntilHungry;

                Vec3d currentPos = entity.ServerPos.XYZ;
                double distance = currentPos.DistanceTo(prevPos) / updates;
                distance = Math.Max(0, distance + currentPos.Y - prevPos.Y); // Climbing/falling adjustment
                // Riding a boat or train shouldn't make the animal hungrier
                if (entity.WatchedAttributes["mountedOn"] != null) {
                    distance = 0;
                }
                prevPos = currentPos;
                float work = 1 + (float)distance / updateSeconds / 10;
                float timespeed = entity.Api.World.Calendar.SpeedOfTime * entity.Api.World.Calendar.CalendarSpeedMul / 30;
                float hungerrate = entity.Stats.GetBlended("hungerrate");
                float saturationConsumed = baseHungerRate * work * hungerrate * timespeed;
                saturationConsumed *= 1 / (1 + MetabolicEfficiency);
                ConsumeSaturation(saturationConsumed);
                // When player sleeps or chunk is unloaded, regain weight but don't starve
                while ((currentHours - lastUpdateHours > 2 * updateRateHours) && (Fullness > -0.8f)) {
                    UpdateCondition(updateRateHours);
                    ConsumeSaturation(saturationConsumed);
                    lastUpdateHours += updateRateHours;
                }
                UpdateCondition(updateRateHours);
                LastUpdateHours = currentHours;
                ApplyNutritionEffects();
            }
        }

        public void UpdateCondition(float hours) {
            // Become fatter or thinner
            float fullness = Fullness;
            float gain = fullness * fullness * fullness;
            float recovery = 1 - AnimalWeight;
            float weightShiftRate = 0.5f * hours / 24 / 12.5f;
            ShiftWeight(weightShiftRate * (gain + recovery) / 2);
        }

        public void ShiftWeight(float deltaWeight) {
            float inefficiency = deltaWeight > 0 ? 1.05f : 0.95f;
            AnimalWeight = (float)Math.Clamp(AnimalWeight + deltaWeight, 0.5f, 2f);
            float fractionOfOwnWeightEatenPerDay = 0.04f;
            float totalSaturation = AdjustedMaxSaturation * 2;
            float deltaSat = deltaWeight * inefficiency / fractionOfOwnWeightEatenPerDay * totalSaturation / DaysUntilHungry;
            ConsumeSaturation(deltaSat);
            Fat.Consume(deltaSat / 4);
            if (deltaWeight > 0) {
                Protein.Consume(deltaSat / 4);
            }
        }

        public void ConsumeSaturation(float amount) {
            Saturation = Math.Clamp(Saturation - amount, -AdjustedMaxSaturation, AdjustedMaxSaturation);
            foreach (Nutrient nutrient in Nutrients) {
                nutrient.Consume(amount);
            }
            ApplyNutritionEffects();
        }

        private void messagePlayer(String langKey, Entity byEntity) {
            String message = Lang.GetUnformatted(langKey).Replace("{entity}", entity.GetName());
            ((byEntity as EntityPlayer)?.Player as IServerPlayer)?.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
        }

        public override void GetInfoText(StringBuilder infotext) {
            base.GetInfoText(infotext);
            double[] hungerBoundaries = new double[] { FULL, NOT_HUNGRY, PECKISH, HUNGRY, VERY_HUNGRY, FAMISHED, STARVING };
            int hungerScore = 0;
            float fullness = Fullness;
            foreach (double b in hungerBoundaries) {
                if (fullness < b) {
                    hungerScore += 1;
                }
                else {
                    break;
                }
            }

            string suffix = entity.IsMale() ? "-male" : "-female";
            string text = VSExtensions.GetLangOptionallySuffixed("genelib:infotext-hunger" + hungerScore.ToString(), suffix);
            infotext.AppendLine(text);
        }

        public override string PropertyName() => Code;
    }
}
