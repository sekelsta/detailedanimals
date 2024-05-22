using System;
using System.Reflection;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Genelib {
    public class AnimalHunger : EntityBehavior {
        public const string Code = "animalhunger";

        public float weanedAge = 0;
        protected ITreeAttribute hungerTree;
        public Nutrient Fiber;
        public Nutrient Sugar;
        public Nutrient Starch;
        public Nutrient Fat;
        public Nutrient Protein;
        public Nutrient Water;
        public Nutrient Minerals;

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

        public AnimalHunger(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes) {
            hungerTree = entity.WatchedAttributes.GetOrAddTreeAttribute("hunger");
            MaxSaturation = typeAttributes["maxsaturation"].AsFloat(15);
            if (typeAttributes.KeyExists("monthsUntilWeaned")) {
                weanedAge = typeAttributes["monthsUntilWeaned"].AsFloat() / entity.World.Calendar.DaysPerMonth;
            }

            Fiber = new Nutrient("fiber", typeAttributes, this);
            Sugar = new Nutrient("sugar", typeAttributes, this);
            Starch = new Nutrient("starch", typeAttributes, this);
            Fat = new Nutrient("fat", typeAttributes, this);
            Protein = new Nutrient("protein", typeAttributes, this);
            Water = new Nutrient("water", typeAttributes, this);
            Minerals = new Nutrient("minerals", typeAttributes, this);
            ApplyNutritionEffects();
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
            return Saturation < MaxSaturation && entity.WatchedAttributes.GetFloat("animalWeight", 1f) < 1.8f;
        }

        public bool WantsFood(ItemStack itemstack) {
            // TODO
            return true;
        }

        public bool WantsEmergencyFood(ItemStack itemstack) {
            // TODO
            return false;
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

/*
        protected virtual void tryEatStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity)
        {
            FoodNutritionProperties nutriProps = GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity);

            if (byEntity.World is IServerWorldAccessor && nutriProps != null)
            {
                TransitionState state = UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish);
                float spoilState = state != null ? state.TransitionLevel : 0;

                float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, byEntity);
                float healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, byEntity);

                byEntity.ReceiveSaturation(nutriProps.Satiety * satLossMul, nutriProps.FoodCategory);

                IPlayer player = null;
                if (byEntity is EntityPlayer) player = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

                slot.TakeOut(1);

                if (nutriProps.EatenStack != null)
                {
                    if (slot.Empty)
                    {
                        slot.Itemstack = nutriProps.EatenStack.ResolvedItemstack.Clone();
                    }
                    else
                    {
                        if (player == null || !player.InventoryManager.TryGiveItemstack(nutriProps.EatenStack.ResolvedItemstack.Clone(), true))
                        {
                            byEntity.World.SpawnItemEntity(nutriProps.EatenStack.ResolvedItemstack.Clone(), byEntity.SidedPos.XYZ);
                        }
                    }
                }

                float healthChange = nutriProps.Health * healthLossMul;

                float intox = byEntity.WatchedAttributes.GetFloat("intoxication");
                byEntity.WatchedAttributes.SetFloat("intoxication", Math.Min(1.1f, intox + nutriProps.Intoxication));

                if (healthChange != 0)
                {
                    byEntity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Internal, Type = healthChange > 0 ? EnumDamageType.Heal : EnumDamageType.Poison }, Math.Abs(healthChange));
                }

                slot.MarkDirty();
                player.InventoryManager.BroadcastHotbarSlot();
            }
        }
*/
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled) {
            // TODO
            handled = EnumHandling.PassThrough;
            return null;
        }

        private void messagePlayer(String langKey, Entity byEntity) {
            String message = Lang.GetUnformatted(langKey).Replace("{entity}", entity.GetName());
            ((byEntity as EntityPlayer)?.Player as IServerPlayer)?.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
        }

        public override string PropertyName() => Code;

        public class Nutrient {
            public readonly string name;
            public readonly float min;
            public readonly float max;
            private readonly AnimalHunger outer;

            public Nutrient(string name, JsonObject typeAttributes, AnimalHunger outer) {
                this.name = name;
                if (typeAttributes.KeyExists(name + "Min")) {
                    min = typeAttributes[name + "Min"].AsFloat();
                }
                else {
                    min = 0;
                }
                if (typeAttributes.KeyExists(name + "Max")) {
                    max = typeAttributes[name + "Max"].AsFloat();
                }
                else {
                    max = 0;
                }
                this.outer = outer;
            }

            public float Level {
                get => outer.hungerTree.GetFloat(name + "Level");
                set {
                    outer.hungerTree.SetFloat(name + "Level", value);
                    outer.entity.WatchedAttributes.MarkPathDirty("hunger");
                }
            }

            public float Fill {
                get {
                    if (min == max) {
                        return Level / outer.MaxSaturation < min ? -1 : float.MaxValue;
                    }
                    return (Level / outer.MaxSaturation - min) / (max - min);
                }
            }
        }
    }
}
