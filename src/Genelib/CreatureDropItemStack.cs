using System;
using Vintagestory.API.Common;

namespace Genelib {
    public enum EnumDropCategory {
        Other = 0,
        Meat = 1,
        Pelt = 2,
        Fat = 3
    }
    public class CreatureDropItemStack : BlockDropItemStack {
        public EnumDropCategory Category = EnumDropCategory.Other;

        public BlockDropItemStack WithAnimalWeight(float animalWeight, float healthyWeight) {
            // Set with the expectation that this will be multipled by AnimalWeight later
            float multiplier = 1 / animalWeight;
            if (Category == EnumDropCategory.Meat) {
                multiplier *= Math.Max(0, animalWeight - 0.3f) / 0.7f * healthyWeight;
                multiplier *= GenelibSystem.Config.MeatMultiplier();
            }
            else if (Category == EnumDropCategory.Pelt) {
                multiplier *= (float)Math.Pow(healthyWeight, 0.6667f) * (1 + animalWeight) / 2;
            }
            else if (Category == EnumDropCategory.Fat) {
                float fatness = Math.Max(0, animalWeight - 0.8f) / 0.2f;
                multiplier *= animalWeight * healthyWeight * fatness * fatness;
                multiplier *= GenelibSystem.Config.MeatMultiplier();
            }

            BlockDropItemStack result = Clone();
            result.Quantity = result.Quantity.Clone(); // BlockDropItemStack.Clone() creates a shallow copy
            result.Quantity.avg *= multiplier;
            return result;
        }
    }
}
