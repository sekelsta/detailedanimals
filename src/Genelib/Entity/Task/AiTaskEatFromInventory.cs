
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Genelib {
    public class AiTaskEatFromInventory : AiTaskUseInventory {
        AnimalHunger hunger;
        // Kept in sync with base.useTimeNow, which we cannot use due to being a private field
        float ourUseTimeNow = 0;
        float ourUseTime = 1;

        public AiTaskEatFromInventory(EntityAgent entity, AnimalHunger hunger) : base(entity) {
            this.hunger = hunger;
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
            base.LoadConfig(taskConfig, aiConfig);
            ourUseTime = taskConfig["useTime"].AsFloat(1.5f);
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

        public override void StartExecute() {
            base.StartExecute();
            ourUseTimeNow = 0;
        }

        public override bool ContinueExecute(float dt) {
            ourUseTimeNow += dt;
            if (ourUseTimeNow < ourUseTime) {
                return base.ContinueExecute(dt);
            }
            hunger.Eat(entity.LeftHandItemSlot);
            return false;
        }
    }
}
