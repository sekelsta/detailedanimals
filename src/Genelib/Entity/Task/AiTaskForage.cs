using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using Genelib.Extensions;

namespace Genelib {
    public class AiTaskForage : AiTaskSeekPoi<IAnimalFoodSource> {
        protected AnimalHunger hungerBehavior;
        protected float looseItemSearchDistance = 10;
        protected float motherSearchDistance = 12;
        protected AnimationMetaData digAnimation;
        protected AnimationMetaData eatAnimation;
        protected AnimationMetaData eatLooseItemsAnimation;
        protected AnimationMetaData currentEatAnimation;
        protected GrazeMethod grazeMethod;
        protected string[] nurseFromEntities;
        public CreatureDiet Diet;
        protected bool soundPlayed = false;
        protected AssetLocation eatSound;
        protected float eatTime;

        public AiTaskForage(EntityAgent entity) : base(entity) {  }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
            base.LoadConfig(taskConfig, aiConfig);
            lastSearchHours = entity.World.Calendar.TotalHours - searchRate * entity.World.Rand.NextSingle();

            Diet = entity.Properties.Attributes["creatureDiet"].AsObject<CreatureDiet>();
            if (Diet == null) {
                entity.Api.Logger.Warning("Creature " + entity.Code.ToShortString() + " has SeekFoodAndEat task but no Diet specified");
            }
            eatTime = taskConfig["eatTime"].AsFloat(1.5f);
            digAnimation = taskConfig.TryGetAnimation("digAnimation");
            eatAnimation = taskConfig.TryGetAnimation("eatAnimation");
            eatLooseItemsAnimation = taskConfig.TryGetAnimation("eatAnimationLooseItems", "eatAnimationSpeedLooseItems");

            string eatsoundstring = taskConfig["eatSound"].AsString(null);
            if (eatsoundstring != null) {
                eatSound = new AssetLocation(eatsoundstring).WithPathPrefix("sounds/");
            }

            if (taskConfig["nurseFromEntities"].Exists) {
                nurseFromEntities = taskConfig["nurseFromEntities"].AsArray<string>();
            }
        }

        public override void AfterInitialize() {
            hungerBehavior = entity.GetBehavior<AnimalHunger>();
            if (hungerBehavior == null) {
                entity.Api.Logger.Warning("forage ai task on " + entity.Code + " with no hunger behavior");
            }
        }

        public override bool ShouldExecute() {
            if (!IsSearchTime()) {
                return false;
            }
            target = null;
            currentEatAnimation = eatAnimation;

            float foodLevel = hungerBehavior.Saturation / hungerBehavior.AdjustedMaxSaturation;
            if (foodLevel < AnimalHunger.HUNGRY) {
                if (hungerBehavior.WantsMilk()) {
                    SeekMilk();
                    if (target != null) {
                        return true;
                    }
                }
                if (hungerBehavior.StartedWeaning()) {
                    // Eat loose items
                    entity.Api.ModLoader.GetModSystem<EntityPartitioning>().WalkEntities(
                        entity.ServerPos.XYZ, looseItemSearchDistance, searchItems, EnumEntitySearchType.Inanimate);
                    if (target != null) {
                        currentEatAnimation = eatLooseItemsAnimation ?? eatAnimation;
                        return true;
                    }
                }
            }

            float thirstLevel = hungerBehavior.Water.Level;
            if (thirstLevel < foodLevel && thirstLevel < 0) {
                SeekWater();
                if (target != null) {
                    return true;
                }
            }
            if (foodLevel < AnimalHunger.HUNGRY) {
                if (hungerBehavior.StartedWeaning()) {
                    SeekFood();
                    if (target != null) {
                        return true;
                    }
                }
                if (hungerBehavior.CanDigestMilk()) {
                    SeekMilk();
                }
            }
            if (target == null) {
                lastSearchHours = entity.World.Calendar.TotalHours;
            }
            return target != null;
        }

        public override void StartExecute() {
            base.StartExecute();
            soundPlayed = false;
        }

        public override void FinishExecute(bool cancelled) {
            base.FinishExecute(cancelled);
            if (!cancelled) {
                cooldownUntilTotalHours = entity.Api.World.Calendar.TotalHours + mincooldownHours + entity.World.Rand.NextDouble() * (maxcooldownHours - mincooldownHours);
            }

            if (currentEatAnimation != null) {
                entity.AnimManager.StopAnimation(currentEatAnimation.Code);
            }
        }

        private bool searchItems(Entity entity) {
            if (entity is EntityItem entityitem && hungerBehavior.WantsFood(entityitem.Itemstack)) {
                target = new LooseItemFoodSource(entityitem);
                return false;
            }
            return true;
        }

        private bool searchMother(Entity entity) {
            if (entity.EntityId == this.entity.WatchedAttributes.GetLong("motherId")
                    || entity.EntityId == this.entity.WatchedAttributes.GetLong("fosterId")) {
                target = new NursingMilkSource(entity);
                return false;
            }
            return true;
        }

        private bool searchFoster(Entity entity) {
            foreach (string nurseFrom in nurseFromEntities) {
                if (entity.WildCardMatch(AssetLocation.Create(nurseFrom, this.entity.Code.Domain))) {
                    target = new NursingMilkSource(entity);
                    this.entity.WatchedAttributes.SetLong("fosterId", entity.EntityId);
                    return false;
                }
            }
            return true;
        }

        protected void SeekMilk() {
            entity.Api.ModLoader.GetModSystem<EntityPartitioning>().WalkEntities(
                entity.ServerPos.XYZ, motherSearchDistance, searchMother, EnumEntitySearchType.Creatures);
            if (target == null && !hungerBehavior.StartedWeaning() && nurseFromEntities != null) {
                entity.Api.ModLoader.GetModSystem<EntityPartitioning>().WalkEntities(
                    entity.ServerPos.XYZ, motherSearchDistance, searchFoster, EnumEntitySearchType.Creatures);
            }
        }

        protected void SeekFood() {
            target = pointsOfInterest.GetNearestPoi(entity.ServerPos.XYZ, 48, IsValidFoodPOI) as IAnimalFoodSource;
            if (target != null) {
                return;
            }
            if (hungerBehavior.EatsGrassOrRoots()) {
                grazeMethod = hungerBehavior.GetGrazeMethod(entity.World.Rand);
                var grass = GrassFoodSource.SearchNear(entity);
                if (grass.IsSuitableFor(entity, grazeMethod) && !RecentlyFailedSeek(grass)) {
                    target = grass;
                    if (grazeMethod == GrazeMethod.Root) {
                        currentEatAnimation = digAnimation;
                    }
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
            if (RecentlyFailedSeek(foodSource)) {
                return false;
            }
            return true;
        }

        protected override void OnArrival() {
            if (currentEatAnimation != null) {
                entity.AnimManager.StartAnimation(currentEatAnimation);
            }
        }

        protected override void TickTargetReached() {
            if (!target.IsSuitableFor(entity, Diet)) {
                done = true;
                return;
            }

            if (target is LooseItemFoodSource foodSource) {
                entity.World.SpawnCubeParticles(entity.ServerPos.XYZ, foodSource.ItemStack, 0.25f, 1, 0.25f + 0.5f * (float)entity.World.Rand.NextDouble());
            }

            if (timeSinceTargetReached > eatTime * 0.75f && !soundPlayed) {
                soundPlayed = true;
                if (eatSound != null) {
                    entity.World.PlaySoundAt(eatSound, entity, null, true, 16, 1);
                }
            }

            if (timeSinceTargetReached >= eatTime) {
                float saturation = target.ConsumeOnePortion(entity);
                hungerBehavior.Eat(null, saturation);
                done = true;
            }
        }
    }
}
