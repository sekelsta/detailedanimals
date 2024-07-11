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
        protected IAnimalFoodSource target = null;
        protected float looseItemSearchDistance = 10;
        protected POIRegistry pointsOfInterest;

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
            target = null;
            lastSearchHours = entity.World.Calendar.TotalHours - entity.World.Rand.NextSingle() * searchRate;

            float hungerLevel = hungerBehavior.Saturation / hungerBehavior.AdjustedMaxSaturation;
            if (hungerLevel > 0) {
                // Eat loose items
                entity.Api.ModLoader.GetModSystem<EntityPartitioning>().WalkEntities(
                    entity.ServerPos.XYZ, looseItemSearchDistance, searchItems, EnumEntitySearchType.Inanimate);
                if (target != null) {
                    return true;
                }
            }

            float thirstLevel = hungerBehavior.Water.Level;
            if (thirstLevel > hungerLevel && thirstLevel > 0) {
                SeekWater();
                if (target != null) {
                    return true;
                }
            }
            if (hungerLevel > 0 && target == null) {
                SeekFood();
            }
            return target != null;
        }

        private bool searchItems(Entity entity) {
            if (entity is EntityItem entityitem && hungerBehavior.WantsFood(entityitem.Itemstack)) {
                target = new LooseItemFoodSource(entityitem);
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
            target = pointsOfInterest.GetNearestPoi(entity.ServerPos.XYZ, 48, IsValidFoodPOI) as IAnimalFoodSource;
            if (target != null) {
                return;
            }
            if (hungerBehavior.EatsGrassOrRoots()) {
                BlockPos blockPos = entity.ServerPos.XYZ.AsBlockPos;
                Random random = entity.World.Rand;
                blockPos.X += random.Next(4) - random.Next(4);
                blockPos.Y += random.Next(6) - random.Next(2);
                blockPos.Z += random.Next(4) - random.Next(4);
                int i = 0;
                while (i < 8 && entity.World.BlockAccessor.GetBlock(blockPos).Id == 0) {
                    ++i;
                    --blockPos.Y;
                }
                GrassFoodSource grass = new GrassFoodSource(blockPos);
                if (grass.IsSuitableFor(entity, Diet)) {
                    target = grass;
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
