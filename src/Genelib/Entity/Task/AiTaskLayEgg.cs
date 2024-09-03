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
                    ItemStack egg = reproduce.GiveEgg();
                    nestbox.AddEgg(entity, egg, incubationDays);
                    laid = true;
                    done = true;
                }
            }
            else if (timeSinceTargetReached >= layTime && !laid) {
                laid = true;
                if (target.TryAddEgg(entity, null, incubationDays)) {
                    PlaySound();
                    done = true;
                }
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
