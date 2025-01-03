using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Genelib {
    public class NursingMilkSource : IAnimalFoodSource {
        public Entity Mother;

        public NursingMilkSource(Entity entity) {
            this.Mother = entity;
        }

        public Vec3d Position => Mother.Pos.XYZ;

        public bool IsSuitableFor(Entity entity, CreatureDiet diet) {
            return Mother.Alive;
        }

        public float ConsumeOnePortion(Entity entity) {
            NutritionData nutrition = NutritionData.Get("milk");
            AnimalHunger hunger = entity.GetBehavior<AnimalHunger>();
            if (hunger == null) {
                return 0;
            }
            float satiety = AnimalHunger.FULL * hunger.AdjustedMaxSaturation - hunger.Saturation;
            if (satiety <= 0) {
                return 0;
            }
            hunger.Eat(nutrition, satiety);
            return 0;
        }

        public string Type => "milk";
    }
}
