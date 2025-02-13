using System;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using Genelib.Extensions;

namespace Genelib {
    public class AiTaskLayEgg : AiTaskSitOnNest {
        protected ReproduceEgg reproduce;
        protected bool laid = false;
        protected float layTime;
        protected double incubationDays;
        protected bool incubationScalesWithMonthLength = true;
        protected double hoursPerEgg;
        protected int failedSearchAttempts = 0;

        public double EggLaidHours {
            get => entity.WatchedAttributes.GetDouble("eggLaidHours");
            set => entity.WatchedAttributes.SetDouble("eggLaidHours", value);
        }

        public AiTaskLayEgg(EntityAgent entity) : base(entity) { }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
            base.LoadConfig(taskConfig, aiConfig);
            layTime = taskConfig["layTime"].AsFloat(1.5f);
            incubationDays = taskConfig["incubationMonths"].AsDouble(1) * entity.World.Calendar.DaysPerMonth;
            hoursPerEgg = taskConfig["hoursPerEgg"].AsDouble(30f);
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
            if (EggLaidHours + hoursPerEgg > entity.World.Calendar.TotalHours) {
                return false;
            }
            if (!reproduce.CanLayEgg()) {
                EggLaidHours = entity.World.Calendar.TotalHours;
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
    }
}
