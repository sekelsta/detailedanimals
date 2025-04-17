using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Genelib {
    public enum EnumDropCategory {
        Unknown = 0,
        Meat = 1,
        Pelt = 2,
        Fat = 3,
        Constant = 4
    }
    public class CreatureDropItemStack : BlockDropItemStack {
        public EnumDropCategory Category = EnumDropCategory.Unknown;

        public BlockDropItemStack WithAnimalWeight(float animalWeight, float healthyWeight) {
            if (Category == EnumDropCategory.Unknown) {
                // Resolve() should have been called ahead of time
                JsonObject jsonCategory = ResolvedItemstack?.Collectible?.Attributes?["productCategory"];
                if (jsonCategory != null && jsonCategory.Exists) {
                    string stringCategory = jsonCategory.AsString();
                    Category = Enum.Parse<EnumDropCategory>(stringCategory, ignoreCase: true);
                }
            }

            // Set with the expectation that this will be multipled by AnimalWeight later
            float multiplier = 1 / animalWeight;
            if (Category == EnumDropCategory.Meat) {
                multiplier *= Math.Max(0, animalWeight - 0.3f) / 0.7f * healthyWeight;
                multiplier *= AnimalConfig.Instance.MeatMultiplier();
            }
            else if (Category == EnumDropCategory.Pelt) {
                multiplier *= (float)Math.Pow(healthyWeight, 0.6667f) * (1 + animalWeight) / 2;
            }
            else if (Category == EnumDropCategory.Fat) {
                float fatness = Math.Max(0, animalWeight - 0.8f) / 0.2f;
                multiplier *= animalWeight * healthyWeight * fatness * fatness;
                multiplier *= AnimalConfig.Instance.MeatMultiplier();
            }

            BlockDropItemStack result = Clone();
            result.Quantity = result.Quantity.Clone(); // BlockDropItemStack.Clone() creates a shallow copy
            result.Quantity.avg *= multiplier;
            return result;
        }
    }
}
