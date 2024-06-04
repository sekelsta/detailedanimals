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
        public const string Code = "animalhunger";

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

        public bool CanEat() {
            return Saturation < MaxSaturation && AnimalWeight < 1.8f;
        }

        public bool WantsFood(ItemStack itemstack) {
            // TODO
            return true;
        }

        public bool WantsEmergencyFood(ItemStack itemstack) {
            return AnimalWeight < 0.7 || (AnimalWeight < 0.85 && Saturation < -0.4 * MaxSaturation);
        }

        // Returns true if it can be consumed by hand-feeding
        // Returns false for water and minerals, as well as for poisonous items and non-edible items
        public bool Edible(ItemStack itemstack) {
            // TODO: Make use of CreatureDiet
            // TODO: Return true if we can consume this or part of this and it's not poisonous
            return itemstack.Collectible.GetNutritionProperties(entity.World, itemstack, entity) != null;
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
            ItemStack itemstack = slot.Itemstack;

            // TODO: Figure out what nutrition to gain
            // TODO: Use reflection to check if PetAI:EntityBehaviorTameable exists and call its onInteract
            // Make sure itemstack doesn't get modified twice
            FoodNutritionProperties nutrition = itemstack.Item.GetNutritionProperties(entity.World, itemstack, entity);
            if (nutrition != null) {
                entity.PlayEntitySound("eat", (fedBy as EntityPlayer)?.Player);
                itemstack.Item.GetType().GetMethod("tryEatStop", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(itemstack.Item, new object[] {1, slot, entity });
            }

            // TODO: Eat the food

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
                float intoxication = entity.WatchedAttributes.GetFloat("intoxication");
                if (intoxication > 0)
                {
                    entity.WatchedAttributes.SetFloat("intoxication", Math.Max(0, intoxication - 0.005f));
                }

                Vec3d currentPos = entity.ServerPos.XYZ;
                double distance = currentPos.DistanceTo(prevPos);
                distance = Math.Max(0, distance + currentPos.Y - prevPos.Y); // Climbing/falling adjustment
                // Riding a boat or train shouldn't make the animal hungrier
                if (entity.WatchedAttributes["mountedOn"] != null) {
                    distance = 0;
                }
                prevPos = currentPos;
                float work = 1 + (float)distance;
                float timespeed = entity.Api.World.Calendar.SpeedOfTime * entity.Api.World.Calendar.CalendarSpeedMul;
                ConsumeSaturation(baseHungerRate * work * entity.Stats.GetBlended("hungerrate") * timespeed);

                // Become fatter or thinner
                float fullness = Saturation / MaxSaturation;
                float gain = fullness * fullness * fullness;
                float recovery = 1 - AnimalWeight;
                float weightShiftRate = 0.025f;
                ShiftWeight(weightShiftRate * (gain + recovery) / 2);
            }
        }

        public void ConsumeSaturation(float amount) {
            Saturation = Math.Clamp(Saturation - amount, -MaxSaturation, MaxSaturation);
            foreach (Nutrient nutrient in Nutrients) {
                nutrient.Consume(amount);
            }
            ApplyNutritionEffects();
        }

        public void ShiftWeight(float deltaWeight) {
            float inefficiency = deltaWeight > 0 ? 0.95f : 1.05f;
            AnimalWeight = (float)Math.Clamp(AnimalWeight + deltaWeight * inefficiency, 0.5f, 2f);
            float dryFractionOfOwnWeightEatenPerDay = 0.01f;
            float wetFractionOfOwnWeightEatenPerDay = 4 * dryFractionOfOwnWeightEatenPerDay;
            float deltaSat = -deltaWeight / wetFractionOfOwnWeightEatenPerDay * MaxSaturation;
            ConsumeSaturation(deltaSat);
        }

        private void messagePlayer(String langKey, Entity byEntity) {
            String message = Lang.GetUnformatted(langKey).Replace("{entity}", entity.GetName());
            ((byEntity as EntityPlayer)?.Player as IServerPlayer)?.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
        }

        public override string PropertyName() => Code;
    }
}
