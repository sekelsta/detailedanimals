using Newtonsoft.Json.Linq;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

using Genelib.Extensions;

namespace DetailedAnimals.Extensions {
    public static class AnimalExtensions {
        public static double BodyCondition(this Entity entity) {
            return entity.WatchedAttributes.TryGetDouble("bodyCondition") ?? entity.WatchedAttributes.GetFloat("bodyCondition", 1);
        }

        public static void SetBodyCondition(this Entity entity, double value) {
            if (double.IsNaN(value)) {
                throw new ArgumentException("Cannot set body condition value to NaN. Entity code: " + entity.Code);
            }
            entity.WatchedAttributes.SetDouble("bodyCondition", value);
            entity.WatchedAttributes.SetFloat("animalWeight", (float)Math.Min(1.08, value));
        }

        public static double ExtraGrowth(this Entity entity) {
            return entity.WatchedAttributes.GetDouble("extraGrowth", 0);
        }

        public static void SetExtraGrowth(this Entity entity, double value) {
            if (double.IsNaN(value)) {
                throw new ArgumentException("Cannot set extra growth value to NaN. Entity code: " + entity.Code);
            }
            entity.WatchedAttributes.SetDouble("extraGrowth", value);
        }

        public static float BaseWeight(this Entity entity) {
            float weight = entity.WatchedAttributes.GetFloat("growthWeightFraction", 1);
            float dimorphism = entity.Properties.Attributes?["weightDimorphism"].AsFloat(0) ?? 0;
            weight *= entity.IsMale() ? 1 + dimorphism : 1 - dimorphism;
            return weight;
        }

        public static double WeightModifier(this Entity entity) {
            double weight = BaseWeight(entity);
            weight *= entity.WatchedAttributes.GetFloat("animalWeight", 1);
            weight += entity.WatchedAttributes.GetDouble("extraGrowth", 0);
            return weight;
        }

        public static float HealthyAdultWeightKg(this Entity entity) {
            float adultWeightKg = entity.Properties?.Attributes?["adultWeightKg"]?.AsFloat(160) ?? 160;
            float dimorphism = entity.Properties.Attributes?["weightDimorphism"].AsFloat(0) ?? 0;
            float weight = entity.IsMale() ? 1 + dimorphism : 1 - dimorphism;
            return weight * adultWeightKg;
        }
    }
}
