using System;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using DetailedAnimals.Extensions;
using Genelib;
using Genelib.Extensions;

namespace DetailedAnimals {
    public class AiTaskLayEgg : AiTaskSitOnNest {
        protected ReproduceEgg reproduce;
        protected bool laid = false;
        protected float layTime;
        protected int failedSearchAttempts = 0;

        public AiTaskLayEgg(EntityAgent entity) : base(entity) { }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
            base.LoadConfig(taskConfig, aiConfig);
            layTime = taskConfig["layTime"].AsFloat(1.5f);
        }

        public override void AfterInitialize() {
            reproduce = entity.GetBehavior<ReproduceEgg>();
            if (reproduce == null) {
                throw new FormatException("No genelib.eggreproduce behavior found for " + entity.Code + " needed by AiTaskLayEgg");
            }
        }

        public override bool ShouldExecute() {
            if (!IsSearchTime()) {
                return false;
            }
            if (!reproduce.CanLayEgg()) {
                return false;
            }

            int searchRadius = 42;
            target = pointsOfInterest.GetWeightedNearestPoi(entity.Pos.XYZ, searchRadius, IsValidNonfullNest) as IAnimalNest;

            if (target == null) {
                if (failedSearchAttempts >= 1) {
                    target = CreateGroundNest();
                }
                else {
                    failedSearchAttempts += 1;
                    cooldownUntilMs = entity.World.ElapsedMilliseconds + 45000 + entity.World.Rand.Next(30000);
                }
            }

            if (target != null) {
                failedSearchAttempts = 0;
            }

            return target != null;
        }

        public override void StartExecute() {
            base.StartExecute();
            laid = false;
        }

        protected bool IsValidNonfullNest(IPointOfInterest poi) {
            if (!IsValidNest(poi)) {
                return false;
            }
            GeneticNest geneticNest = poi as GeneticNest;
            if (geneticNest != null) {
                return !geneticNest.Full();
            }
            return true;
        }

        public IAnimalNest CreateGroundNest() {
            string nestCode = "genelib:nest-ground";
            Block block = entity.World.GetBlock(nestCode);
            if (block == null) {
                throw new ArgumentException(entity.Code.ToString() + " attempted to create nest block of type " 
                        + nestCode + " but no such block was found.");
            }

            BlockPos pos = entity.Pos.XYZ.AsBlockPos;
            IBlockAccessor blockAccess = entity.World.BlockAccessor;
            if (blockAccess.GetBlock(pos, BlockLayersAccess.Fluid).IsLiquid()
                    || !blockAccess.GetBlock(pos).IsReplacableBy(block)) {
                return null;
            }
            BlockPos below = pos.DownCopy();
            if (!blockAccess.GetMostSolidBlock(below).CanAttachBlockAt(blockAccess, block, below, BlockFacing.UP)) {
                return null;
            }
            blockAccess.SetBlock(block.BlockId, pos);

            // This seems to only be used by torches, but vanilla checks it so let's do that too
            BlockEntityTransient transient = blockAccess.GetBlockEntity(pos) as BlockEntityTransient;
            if (transient != null) {
                transient.SetPlaceTime(entity.World.Calendar.TotalHours);
                if (transient.IsDueTransition()) {
                    blockAccess.SetBlock(0, pos);
                    return null;
                }
            }

            return blockAccess.GetBlockEntity(pos) as IAnimalNest;
        }

        protected override void OnArrival() {
            target.SetOccupier(entity);
            if (sitAnimation != null) {
                entity.AnimManager.StartAnimation(sitAnimation);
            }
        }

        protected override void TickTargetReached() {
            GeneticNest nest = target as GeneticNest;
            if (nest != null) {
                if (nest.Full()) {
                    laid = true;
                    return;
                }
                if (timeSinceTargetReached >= layTime) {
                    if (!laid) {
                        PlaySound();
                        laid = true;
                        done = true;
                        ItemStack egg = reproduce.LayEgg();
                        nest.AddEgg(entity, egg);
                    }
                    if (nest.CountEggs() == 0) {
                        done = true;
                    }
                }
            }
            else if (timeSinceTargetReached >= layTime && !laid) {
                if (target.TryAddEgg(entity, null, reproduce.IncubationDays)) {
                    PlaySound();
                    laid = true;
                    done = true;
                    reproduce.LayEgg();
                }
            }
        }
    }
}
