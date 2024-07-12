using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Genelib {
    // Would like to reuse more code from AiTaskSeekFoodAndEat, but almost all its members are private
    public class AiTaskForage : AiTaskSeekFoodAndEat {
        protected AnimalHunger hungerBehavior;
        protected double lastSearchHours;
        protected float searchRate = 0.25f;
        protected float looseItemSearchDistance = 10;
        protected POIRegistry pointsOfInterest;

        protected IAnimalFoodSource Target {
            get => (IAnimalFoodSource) typeof(AiTaskSeekFoodAndEat).GetField("targetPoi", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this);
            set => typeof(AiTaskSeekFoodAndEat).GetField("targetPoi", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, value);
        }

        public AiTaskForage(EntityAgent entity) : base(entity) { 
            pointsOfInterest = entity.Api.ModLoader.GetModSystem<POIRegistry>();
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
            base.LoadConfig(taskConfig, aiConfig);
            lastSearchHours = entity.World.Calendar.TotalHours - searchRate * entity.World.Rand.NextSingle();
        }

        public override void AfterInitialize() {
            hungerBehavior = entity.GetBehavior<AnimalHunger>();
        }

        public override bool ShouldExecute() {
            if (!IsSearchTime()) {
                return false;
            }
            Target = null;

            float foodLevel = hungerBehavior.Saturation / hungerBehavior.AdjustedMaxSaturation;
            // TODO: Better test for whether entity is hungry
            if (foodLevel < 0) {
                // Eat loose items
                entity.Api.ModLoader.GetModSystem<EntityPartitioning>().WalkEntities(
                    entity.ServerPos.XYZ, looseItemSearchDistance, searchItems, EnumEntitySearchType.Inanimate);
                if (Target != null) {
                    return true;
                }
            }

            float thirstLevel = hungerBehavior.Water.Level;
            if (thirstLevel < foodLevel && thirstLevel < 0) {
                SeekWater();
                if (Target != null) {
                    return true;
                }
            }
            if (foodLevel < 0) {
                SeekFood();
            }
            if (Target == null) {
                lastSearchHours = entity.World.Calendar.TotalHours;
            }
            return Target != null;
        }

        public override void FinishExecute(bool cancelled) {
            // Base method resets cooldown if quantityEaten is 0
            base.FinishExecute(cancelled);
            if (!cancelled) {
                cooldownUntilTotalHours = entity.Api.World.Calendar.TotalHours + mincooldownHours + entity.World.Rand.NextDouble() * (maxcooldownHours - mincooldownHours);
            }
        }

        private bool searchItems(Entity entity) {
            if (entity is EntityItem entityitem && hungerBehavior.WantsFood(entityitem.Itemstack)) {
                Target = new LooseItemFoodSource(entityitem);
                return false;
            }
            return true;
        }

        protected bool IsSearchTime() {
            return lastSearchHours + searchRate <= entity.World.Calendar.TotalHours
                && cooldownUntilMs <= entity.World.ElapsedMilliseconds
                && cooldownUntilTotalHours <= entity.World.Calendar.TotalHours
                && EmotionStatesSatisifed();
        }

        protected void SeekMilk() {
            // TODO
        }

        protected void SeekFood() {
            Target = pointsOfInterest.GetNearestPoi(entity.ServerPos.XYZ, 48, IsValidFoodPOI) as IAnimalFoodSource;
            if (Target != null) {
                return;
            }
            if (hungerBehavior.EatsGrassOrRoots()) {
                var grass = GrassFoodSource.SearchNear(entity);
                if (grass.IsSuitableFor(entity, Diet)) {
                    Target = grass;
                    return;
                }
            }
            // TODO: Maybe deer should browse on leaves?
        }

        protected void SeekWater() {
            // TODO
        }

        protected bool IsValidFoodPOI(IPointOfInterest poi) {
            if (poi.Type != "food") {
                return false;
            }
            IAnimalFoodSource foodSource = poi as IAnimalFoodSource;
            if (foodSource == null) {
                return false;
            }
            if (!foodSource.IsSuitableFor(entity, Diet)) {
                return false;
            }
            // TODO: More thorough checking that the entity can eat the food
            if (RecentlyFailedSeek(poi)) {
                return false;
            }
            return true;
        }

        protected bool RecentlyFailedSeek(IPointOfInterest poi) {
            FieldInfo fieldInfo = typeof(AiTaskSeekFoodAndEat).GetField("failedSeekTargets", BindingFlags.NonPublic | BindingFlags.Instance);
            IDictionary dict = (IDictionary)fieldInfo.GetValue(this);
            object failedSeek = dict[poi];
            if (failedSeek == null) {
                return false;
            }
            int count = (int) failedSeek.GetType().GetField("Count", BindingFlags.Public | BindingFlags.Instance).GetValue(failedSeek);
            if (count < 4) {
                return false;
            }
            long lastTryMs = (long) failedSeek.GetType().GetField("LastTryMs", BindingFlags.Public | BindingFlags.Instance).GetValue(failedSeek);
            return lastTryMs >= entity.World.ElapsedMilliseconds - 60000;
        }
    }
}
