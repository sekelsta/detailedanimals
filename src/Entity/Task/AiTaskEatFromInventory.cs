
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace DetailedAnimals {
    public class AiTaskEatFromInventory : AiTaskUseInventory {
        AnimalHunger hunger;

        public AiTaskEatFromInventory(EntityAgent entity, AnimalHunger hunger) : base(entity) {
            this.hunger = hunger;
        }

        public override bool ShouldExecute() {
            if (!(cooldownUntilMs <= entity.World.ElapsedMilliseconds
                    && cooldownUntilTotalHours <= entity.World.Calendar.TotalHours
                    && PreconditionsSatisifed())) {
                return false;
            }

            if (entity.LeftHandItemSlot == null || entity.LeftHandItemSlot.Empty) {
                return false;
            }

            if (!hunger.CanEat(entity.LeftHandItemSlot.Itemstack)) {
                if (entity.World.Rand.NextSingle() < 0.4f) {
                    entity.World.SpawnItemEntity(entity.LeftHandItemSlot.TakeOutWhole(), entity.ServerPos.XYZ);
                }
                else {
                    cooldownUntilTotalHours = entity.World.Calendar.TotalHours + 0.05f;
                }
                return false;
            }
            return true;
        }

        public override bool ContinueExecute(float dt) {
            if (useTimeNow + dt < useTime) {
                return base.ContinueExecute(dt);
            }
            hunger.Eat(entity.LeftHandItemSlot);
            return false;
        }
    }
}
