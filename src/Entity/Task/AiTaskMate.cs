using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace DetailedAnimals {
    public class AiTaskMate : AiTaskGotoEntity {
        public AiTaskMate(EntityAgent entity, Entity target)  : base(entity, target) {
            allowedExtraDistance = 0.2f;
        }

        public void SetPriority(float value) {
            this.priority = value;
            this.priorityForCancel = value;
        }

        public override bool ContinueExecute(float dt) {
            bool result = base.ContinueExecute(dt);
            // Optimization here: Base function runs logic equivalent to TargetReached() and includes it in result
            if (!result && TargetReached()) {
                Reproduce reproduce = entity.GetBehavior<Reproduce>();
                if (reproduce != null) {
                    reproduce.MateWith(targetEntity);
                }
            }
            return result;
        }
    }
}
