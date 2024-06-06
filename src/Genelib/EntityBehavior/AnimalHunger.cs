using System;
using System.Collections.Generic;
using System.Reflection;

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

        public float weanedAge = 0;
        public float baseHungerRate;
        protected internal ITreeAttribute hungerTree;
        public Nutrient Fiber;
        public Nutrient Sugar;
        public Nutrient Starch;
        public Nutrient Fat;
        public Nutrient Protein;
        public Nutrient Water;
        public Nutrient Minerals;
        public List<Nutrient> Nutrients;

        protected long listenerID;
        protected int accumulator;
        protected Vec3d prevPos;

        public float Saturation {
            get => hungerTree.GetFloat("saturation");
            set {
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

        public float AnimalWeight {
            get => entity.WatchedAttributes.GetFloat("animalWeight", 1f);
            set {
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
            MaxSaturation = typeAttributes["maxsaturation"].AsFloat(15);
            // Takes one day to empty the hunger bar from max
            baseHungerRate = MaxSaturation / 240;
            if (typeAttributes.KeyExists("monthsUntilWeaned")) {
                weanedAge = typeAttributes["monthsUntilWeaned"].AsFloat() / entity.World.Calendar.DaysPerMonth;
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
            listenerID = entity.World.RegisterGameTickListener(SlowTick, 12000);
            ApplyNutritionEffects();
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);
            entity.World.UnregisterGameTickListener(listenerID);
        }

        public virtual void ApplyNutritionEffects() {
            // TODO: give bonuses
            // fiber: metabolic efficiency, max health++
            // sugar: speed, jump height, energy
            // starch: speed, stamina, metabolic efficiency
            // fat: growth rate, strength, milk production, defense
            // protein: strength, attack power, speed, growth rate, meat drops
            // water: stamina, max health, growth rate, milk production
            // minerals: growth rate, milk production, stamina
        }

        public double Weaned() {
            double age = entity.World.Calendar.TotalDays - entity.WatchedAttributes.GetFloat("birthTotalDays", -99999);
            return age / weanedAge;
        }

        public bool MatchesDiet(ItemStack itemstack) {
            return entity.Properties.Attributes["creatureDiet"].AsObject<CreatureDiet>().Matches(itemstack);
        }

        // Returns true if the animal wants some sort of food right now
        public bool CanEat() {
            return Saturation < MaxSaturation && AnimalWeight < 1.8f;
        }

        // Returns true if this is the sort of food the animal wants right now
        public bool WantsFood(ItemStack itemstack) {
            // TODO
            return true;
            //return itemstack.Collectible.GetNutritionProperties(entity.World, itemstack, entity) != null;
        }

        public bool WantsEmergencyFood(ItemStack itemstack) {
            // TODO: Exclude items inedible even in emergencies, like dry grass for a cat
            return AnimalWeight < 0.7 || (AnimalWeight < 0.85 && Saturation < -0.4 * MaxSaturation);
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled) {
            if (slot.Empty) {
                handled = EnumHandling.PassThrough;
                return;
            }

            ItemStack itemstack = slot.Itemstack;
            if (!MatchesDiet(itemstack)) {
                if (WantsEmergencyFood(itemstack)) {
                    Eat(slot, byEntity);
                    handled = EnumHandling.PreventSubsequent;
                    return;
                }
                handled = EnumHandling.PassThrough;
                return;
            }
            if (!CanEat()) {
                messagePlayer("genelib:message-nothungry", byEntity);
                handled = EnumHandling.PassThrough;
                return;
            }
            if (!WantsFood(itemstack)) {
                messagePlayer("genelib:message-wrongnutrients", byEntity);
                handled = EnumHandling.PassThrough;
                return;
            }
            Eat(slot, byEntity);
            handled = EnumHandling.PreventSubsequent;
            return;
        }

        public virtual void Eat(ItemSlot slot, Entity fedBy) {
            if (entity.World.Side != EnumAppSide.Server) {
                return;
            }
            // Game code does it like this, assuming because we can't trust that fedBy.Player will be synchronized
            IPlayer player = fedBy.World.PlayerByUid((fedBy as EntityPlayer)?.PlayerUID);
            entity.PlayEntitySound("eat", player);

            EntityAgent agent = entity as EntityAgent;
            ItemStack itemstack = slot.Itemstack;
            string[] foodTags = itemstack.Collectible.Attributes?["foodTags"].AsArray<string>();
            NutritionData data = null;
            foreach (string tag in foodTags) {
                NutritionData tagData = NutritionData.Get(tag);
                if (data == null || (tagData != null && tagData.Priority > data.Priority)) {
                    data = tagData;
                }
            }

            // Based on Collectible.tryEatStop
            FoodNutritionProperties nutriProps = itemstack.Collectible.GetNutritionProperties(entity.World, itemstack, entity);
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

            float satiety = nutriProps?.Satiety ?? 100; // TODO
            satiety *= satLossMul;
            float currentSaturation = Saturation;
            agent?.ReceiveSaturation(satiety, data?.FoodCategory ?? EnumFoodCategory.NoNutrition);
            float maxsat = MaxSaturation;
            satiety = satiety / 100;  // Approximate conversion between numbers used for player hunger and by troughs
            Saturation = Math.Clamp(currentSaturation + satiety, -maxsat, maxsat);
            float gain = satiety / maxsat;
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
                player.InventoryManager.BroadcastHotbarSlot();
            }

            ApplyNutritionEffects();
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled) {
            // TODO
            handled = EnumHandling.PassThrough;
            return null;
        }

        private void SlowTick(float dt)
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
            // Don't put in SlowTick, because that still gets called when game is paused
            // And don't reference game calendar, because entities in unloaded chunks have no way to eat and so should 
            // not get hungry.

            ++accumulator;
            int TPS = 30;
            if (accumulator > 12 * TPS) {
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

                Vec3d currentPos = entity.ServerPos.XYZ;
                double distance = currentPos.DistanceTo(prevPos) / updates;
                distance = Math.Max(0, distance + currentPos.Y - prevPos.Y); // Climbing/falling adjustment
                // Riding a boat or train shouldn't make the animal hungrier
                if (entity.WatchedAttributes["mountedOn"] != null) {
                    distance = 0;
                }
                prevPos = currentPos;
                float work = 1 + (float)distance;
                float timespeed = entity.Api.World.Calendar.SpeedOfTime * entity.Api.World.Calendar.CalendarSpeedMul;
                float saturationConsumed = baseHungerRate * work * entity.Stats.GetBlended("hungerrate") * timespeed;
                ConsumeSaturation(saturationConsumed);

                // When player sleeps or chunk is unloaded, regain weight but don't starve
                while ((currentHours - lastUpdateHours > 2 * updateRateHours) && (Saturation / MaxSaturation > -0.8f)) {
                    UpdateCondition(updateRateHours);
                    ConsumeSaturation(saturationConsumed);
                    lastUpdateHours += updateRateHours;
                }
                UpdateCondition(updateRateHours);
                LastUpdateHours = currentHours;
            }
        }

        public void UpdateCondition(float hours) {
            // Become fatter or thinner
            float fullness = Saturation / MaxSaturation;
            float gain = fullness * fullness * fullness;
            float recovery = 1 - AnimalWeight;
            float weightShiftRate = 0.5f * hours / 24 / 10;
            ShiftWeight(weightShiftRate * (gain + recovery) / 2);
        }

        public void ShiftWeight(float deltaWeight) {
            float inefficiency = deltaWeight > 0 ? 0.95f : 1.05f;
            AnimalWeight = (float)Math.Clamp(AnimalWeight + deltaWeight * inefficiency, 0.5f, 2f);
            float dryFractionOfOwnWeightEatenPerDay = 0.01f;
            float wetFractionOfOwnWeightEatenPerDay = 4 * dryFractionOfOwnWeightEatenPerDay;
            float deltaSat = -deltaWeight / wetFractionOfOwnWeightEatenPerDay * MaxSaturation;
            ConsumeSaturation(deltaSat);
            Fat.Consume(deltaSat / 4);
            if (deltaWeight > 0) {
                Protein.Consume(deltaSat / 4);
            }
        }

        public void ConsumeSaturation(float amount) {
            Saturation = Math.Clamp(Saturation - amount, -MaxSaturation, MaxSaturation);
            foreach (Nutrient nutrient in Nutrients) {
                nutrient.Consume(amount);
            }
            ApplyNutritionEffects();
        }

        private void messagePlayer(String langKey, Entity byEntity) {
            String message = Lang.GetUnformatted(langKey).Replace("{entity}", entity.GetName());
            ((byEntity as EntityPlayer)?.Player as IServerPlayer)?.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
        }

        public override string PropertyName() => Code;
    }
}
