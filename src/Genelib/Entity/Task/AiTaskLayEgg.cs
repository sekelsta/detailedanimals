using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
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

        public AiTaskLayEgg(EntityAgent entity) : base(entity) {
            reproduce = entity.GetBehavior<Reproduce>();
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
            base.LoadConfig(taskConfig, aiConfig);
            sitAnimation = taskConfig.TryGetAnimation("sitAnimation");
            sitSessionHours = taskConfig["sitDays"].AsFloat(1f) * entity.World.Calendar.HoursPerDay;
            layTime = taskConfig["layTime"].AsFloat(1.5f);
            incubationDays = taskConfig["incubationMonths"].AsDouble(1) * entity.World.Calendar.DaysPerMonth;
            hoursPerEgg = taskConfig["hoursPerEgg"].AsDouble(30f);
        }

        public override bool ShouldExecute() {
            if (!IsSearchTime()) {
                return false;
            }
            if (EggLaidHours + hoursPerEgg > entity.World.Calendar.TotalHours) {
                return false;
            }
            if (!Reproduce.EntityCanMate(entity)) {
                return false;
            }

            int searchRadius = 42;
            target = pointsOfInterest.GetWeightedNearestPoi(entity.ServerPos.XYZ, searchRadius, IsValidNest) as IAnimalNest;

            if (target == null) {
                // TODO: Lay egg on ground
                return false;
            }

            return true;
        }

        protected bool IsValidNest(IPointOfInterest poi) {
            if (poi.Type != "nest") {
                return false;
            }
            IAnimalNest nest = poi as IAnimalNest;
            if (nest == null) {
                return false;
            }
            if (!nest.IsSuitableFor(entity) || nest.Occupied(entity)) {
                return false;
            }
            if (RecentlyFailedSeek(nest)) {
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
            return target.Occupied(entity);
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
            GeneticNestbox nestbox = target as GeneticNestbox;
            if (nestbox != null) {
                if (nestbox.Full()) {
                    return;
                }
                if (timeSinceTargetReached >= layTime && !laid) {
                    PlaySound();
                    laid = true;
                    done = true;
                    EggLaidHours = entity.World.Calendar.TotalHours;
                    ItemStack egg = reproduce.GiveEgg();
                    egg.Attributes.SetDouble("incubationHoursRemaining", incubationDays * 24 * GeneticsModSystem.AnimalGrowthTime);
                    // If incubation length scales with month length, freshness should too
                    if (incubationScalesWithMonthLength) {
                        TransitionState[] transitions = egg.Collectible?.UpdateAndGetTransitionStates(entity.World, new DummySlot(egg));
                        // Note calling UpdateAndGetTransitionStates may set the itemstack to null e.g. if it rotted with 50% conversion rate
                        if (transitions != null && egg.Collectible != null) {
                            for (int i = 0; i < transitions.Length; ++i) {
                                if (transitions[i].Props.Type == EnumTransitionType.Perish) {
                                    ITreeAttribute attr = (ITreeAttribute)egg.Attributes["transitionstate"];
                                    (attr["freshHours"] as FloatArrayAttribute).value[i] *= entity.World.Calendar.DaysPerMonth / 9f;
                                }
                            }
                        }
                    }
                    nestbox.AddEgg(entity, egg);
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
