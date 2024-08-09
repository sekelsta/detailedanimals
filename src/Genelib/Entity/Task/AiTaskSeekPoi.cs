using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Genelib {
    public abstract class AiTaskSeekPoi<T> : AiTaskBase where T : IPointOfInterest {
        protected POIRegistry pointsOfInterest;
        protected float moveSpeed;
        protected bool nowStuck = false;
        protected double lastSearchHours;
        protected float searchRate = 0.25f;
        protected Dictionary<T, long> failedSeekTargets = new Dictionary<T, long>();
        protected T target;
        protected float halfTargetWidth = 0.6f; // TODO: Use 0 for chickens nesting, 0.6 for animals eating from trough
        protected float timeSinceTargetReached;

        public AiTaskSeekPoi(EntityAgent entity)  : base(entity) {
            pointsOfInterest = entity.Api.ModLoader.GetModSystem<POIRegistry>();
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
            moveSpeed = taskConfig["movespeed"].AsFloat(0.02f);
        }

        protected bool IsSearchTime() {
            return lastSearchHours + searchRate <= entity.World.Calendar.TotalHours
                && cooldownUntilMs <= entity.World.ElapsedMilliseconds
                && cooldownUntilTotalHours <= entity.World.Calendar.TotalHours
                && EmotionStatesSatisifed();
        }

        protected bool RecentlyFailedSeek(T poi) {
            long time;
            failedSeekTargets.TryGetValue(poi, out time);
            return time + 60000 > entity.World.ElapsedMilliseconds;
        }

        public override void StartExecute() {
            base.StartExecute();
            nowStuck = false;
            timeSinceTargetReached = 0;
            pathTraverser.NavigateTo_Async(target.Position, moveSpeed, MinDistanceToTarget() / 2, OnGoalReached, OnStuck, null, 1000, 1);
        }

        public override bool CanContinueExecute() {
            return pathTraverser.Ready;
        }

        public float MinDistanceToTarget() {
            return halfTargetWidth + entity.SelectionBox.XSize / 2 + 0.05f;
        }

        public override bool ContinueExecute(float dt) {
            Vec3d pos = target.Position;
            double distance = pos.HorizontalSquareDistanceTo(entity.ServerPos.X, entity.ServerPos.Z);

            pathTraverser.CurrentTarget.X = pos.X;
            pathTraverser.CurrentTarget.Y = pos.Y;
            pathTraverser.CurrentTarget.Z = pos.Z;         

            float minDist = MinDistanceToTarget();
            if (distance <= minDist * minDist) {
                pathTraverser.Stop();
                if (animMeta != null) {
                    entity.AnimManager.StopAnimation(animMeta.Code);
                }

                failedSeekTargets.Remove(target);
                timeSinceTargetReached += dt;
                if (!OnTargetReached()) {
                    return false;
                }
            }
            else {
                if (!pathTraverser.Active) {
                    float rndx = (float)entity.World.Rand.NextDouble() * 0.3f - 0.15f;
                    float rndz = (float)entity.World.Rand.NextDouble() * 0.3f - 0.15f;
                    pathTraverser.NavigateTo(target.Position.AddCopy(rndx, 0, rndz), moveSpeed, MinDistanceToTarget() - 0.15f, OnGoalReached, OnStuck, false, 500);
                }
            }

            if (nowStuck) {
                return false;
            }

            return true;
        }


        public override void FinishExecute(bool cancelled) {
            base.FinishExecute(cancelled);
            pathTraverser.Stop();

            if (animMeta != null) {
                entity.AnimManager.StopAnimation(animMeta.Code);
            }
        }

        protected void OnStuck() {
            nowStuck = true;
            failedSeekTargets[target] = entity.World.ElapsedMilliseconds;            
        }

        protected void OnGoalReached() {
            failedSeekTargets.Remove(target);
        }

        protected abstract bool OnTargetReached();
    }
}
