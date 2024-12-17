using System;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using Genelib.Extensions;

namespace Genelib {
    public class AiTaskLayEgg : AiTaskSeekPoi<IAnimalNest> {
        protected Reproduce reproduce;
        protected AnimationMetaData sitAnimation;
        protected double sitEndHour;
        protected double sitSessionHours;
        protected bool laid = false;
        protected float layTime;
        protected double incubationDays;
        protected bool incubationScalesWithMonthLength = true;
        protected double hoursPerEgg;

        public double EggLaidHours {
            get => entity.WatchedAttributes.GetDouble("eggLaidHours");
            set => entity.WatchedAttributes.SetDouble("eggLaidHours", value);
        }

        public AiTaskLayEgg(EntityAgent entity) : base(entity) { }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
            base.LoadConfig(taskConfig, aiConfig);
            sitAnimation = taskConfig.TryGetAnimation("sitAnimation");
            sitSessionHours = taskConfig["sitDays"].AsFloat(1f) * entity.World.Calendar.HoursPerDay;
            layTime = taskConfig["layTime"].AsFloat(1.5f);
            incubationDays = taskConfig["incubationMonths"].AsDouble(1) * entity.World.Calendar.DaysPerMonth;
            hoursPerEgg = taskConfig["hoursPerEgg"].AsDouble(30f);
        }

        public override void AfterInitialize() {
            reproduce = entity.GetBehavior<Reproduce>();
            if (reproduce == null) {
                throw new FormatException("No reproduce behavior found for " + entity.Code + " needed by AiTaskLayEgg");
            }
        }

        public override bool ShouldExecute() {
            if (!IsSearchTime()) {
                return false;
            }
            if (EggLaidHours + hoursPerEgg > entity.World.Calendar.TotalHours) {
                return false;
            }
            if (!reproduce.CanLayEgg()) {
                EggLaidHours = entity.World.Calendar.TotalHours;
                return false;
            }

            int searchRadius = 42;
            target = pointsOfInterest.GetWeightedNearestPoi(entity.ServerPos.XYZ, searchRadius, IsValidNest) as IAnimalNest;

            if (target == null) {
                target = CreateGroundNest();
            }

            return target != null;
        }

        public IAnimalNest CreateGroundNest() {
            string nestCode = "genelib:nest-ground";
            Block block = entity.World.GetBlock(nestCode);
            if (block == null) {
                throw new ArgumentException(entity.Code.ToString() + " attempted to create nest block of type " 
                        + nestCode + " but no such block was found.");
            }

            BlockPos pos = entity.ServerPos.XYZ.AsBlockPos;
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

        public override void StartExecute() {
            base.StartExecute();
            laid = false;
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
                if (nest.Full()) {
                    laid = true;
                    return;
                }
                if (timeSinceTargetReached >= layTime) {
                    if (!laid) {
                        PlaySound();
                        laid = true;
                        done = true;
                        EggLaidHours = entity.World.Calendar.TotalHours;
                        ItemStack egg = reproduce.GiveEgg();
                        double incubationHoursTotal = incubationDays * 24 * GenelibSystem.AnimalGrowthTime;
                        egg.Attributes.SetDouble("incubationHoursRemaining", incubationHoursTotal);
                        egg.Attributes.SetDouble("incubationHoursTotal", incubationHoursTotal);
                        // If incubation length scales with month length, freshness should too
                        if (incubationScalesWithMonthLength) {
                            TransitionState[] transitions = egg.Collectible?.UpdateAndGetTransitionStates(entity.World, new DummySlot(egg));
                            // Note calling UpdateAndGetTransitionStates may set the itemstack to null e.g. if it rotted with 50% conversion rate
                            if (transitions != null && egg.Collectible != null) {
                                for (int i = 0; i < transitions.Length; ++i) {
                                    if (transitions[i].Props.Type == EnumTransitionType.Perish) {
                                        ITreeAttribute attr = (ITreeAttribute)egg.Attributes["transitionstate"];
                                        float[] freshHours = (attr["freshHours"] as FloatArrayAttribute).value;
                                        float adjusted = freshHours[i] * entity.World.Calendar.DaysPerMonth / 9f * GlobalConstants.PerishSpeedModifier;
                                        freshHours[i] = (float)Math.Max(adjusted, 6 * 24 + incubationHoursTotal);
                                    }
                                }
                            }
                        }
                        nest.AddEgg(entity, egg);
                    }
                    if (nest.CountEggs() == 0) {
                        done = true;
                    }
                }
            }
            else if (timeSinceTargetReached >= layTime && !laid) {
                if (target.TryAddEgg(entity, null, incubationDays)) {
                    PlaySound();
                    laid = true;
                    done = true;
                    EggLaidHours = entity.World.Calendar.TotalHours;
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
                        entity.World.PlaySoundAt(sound, entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z, null, true, soundRange);
                        lastSoundTotalMs = entity.World.ElapsedMilliseconds;
                    }, soundStartMs);
                }
                else {
                    entity.World.PlaySoundAt(sound, entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z, null, true, soundRange);
                    lastSoundTotalMs = entity.World.ElapsedMilliseconds;
                }
            }
        }
    }
}
