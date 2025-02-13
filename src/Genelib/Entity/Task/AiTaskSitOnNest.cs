using System;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using Genelib.Extensions;

namespace Genelib {
    public class AiTaskSitOnNest : AiTaskSeekPoi<IAnimalNest> {
        protected AnimationMetaData sitAnimation;
        protected double sitEndHour;
        protected double sitSessionHours;

        public AiTaskSitOnNest(EntityAgent entity) : base(entity) { }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
            base.LoadConfig(taskConfig, aiConfig);
            sitAnimation = taskConfig.TryGetAnimation("sitAnimation");
            sitSessionHours = taskConfig["sitDays"].AsFloat(1f) * entity.World.Calendar.HoursPerDay;
        }

        public override bool ShouldExecute() {
            if (!IsSearchTime()) {
                return false;
            }
            if (entity.BodyCondition() < DetailedHarvestable.MALNOURISHED) {
                return false;
            }

            int searchRadius = 42;
            target = pointsOfInterest.GetWeightedNearestPoi(entity.Pos.XYZ, searchRadius, IsValidFullNest) as IAnimalNest;

            return target != null;
        }

        protected bool IsValidFullNest(IPointOfInterest poi) {
            if (!IsValidNest(poi)) {
                return false;
            }
            GeneticNest geneticNest = poi as GeneticNest;
            if (geneticNest != null) {
                return geneticNest.Full();
            }
            BlockEntityHenBox henbox = poi as BlockEntityHenBox;
            if (henbox != null) {
                return henbox.Block.LastCodePart() == "3eggs";
            }
            return false;
        }

        protected bool IsValidNest(IPointOfInterest poi) {
            if (poi.Type != "nest") {
                return false;
            }
            IAnimalNest nest = poi as IAnimalNest;
            if (nest == null) {
                return false;
            }
            if (!nest.IsSuitableFor(entity) || !IsStillValidNest(nest)) {
                return false;
            }
            if (RecentlyFailedSeek(nest)) {
                return false;
            }
            return true;
        }

        protected bool IsStillValidNest(IAnimalNest nest) {
            if (nest.Occupied(entity)) {
                return false;
            }
            if ((nest as GeneticNest)?.ContainsRot() == true) {
                return false;
            }
            return true;
        }

        public override float MinDistanceToTarget() {
            return 0.16f;
        }

        protected override bool ShouldAbort() {
            return !IsStillValidNest(target);
        }

        protected override void OnArrival() {
            target.SetOccupier(entity);
            
            if (sitAnimation != null) {
                entity.AnimManager.StartAnimation(sitAnimation);
                sitEndHour = entity.World.Calendar.TotalHours + sitSessionHours;
            }
        }

        protected override void TickTargetReached() {
            if (sitEndHour <= entity.World.Calendar.TotalHours) {
                done = true;
                return;
            }
            GeneticNest nest = target as GeneticNest;
            if (nest != null) {
                if (nest.ContainsRot()) {
                    done = true;
                    return;
                }
                if (nest.CountEggs() == 0) {
                    done = true;
                    return;
                }
            }
        }

        public override void FinishExecute(bool cancelled) {
            base.FinishExecute(cancelled);

            if (sitAnimation != null) {
                entity.AnimManager.StopAnimation(sitAnimation.Code);
            }

            if (target != null && !target.Occupied(entity)) {
                target.SetOccupier(null);
            }
        }

        protected void PlaySound() {
            if (sound != null) {
                if (soundStartMs > 0) {
                    entity.World.RegisterCallback((dt) => {
                        entity.World.PlaySoundAt(sound, entity.Pos.X, entity.Pos.Y, entity.Pos.Z, null, true, soundRange);
                        lastSoundTotalMs = entity.World.ElapsedMilliseconds;
                    }, soundStartMs);
                }
                else {
                    entity.World.PlaySoundAt(sound, entity.Pos.X, entity.Pos.Y, entity.Pos.Z, null, true, soundRange);
                    lastSoundTotalMs = entity.World.ElapsedMilliseconds;
                }
            }
        }
    }
}
