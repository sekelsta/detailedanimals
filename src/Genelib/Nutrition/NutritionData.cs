using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Genelib {
    public class NutritionData {
        public static string[] Nutrients = new string[] { "fiber", "sugar", "starch", "fat", "protein", "water", "minerals" };

        // Uses string instead of AssetLocation because here mod collisions are better than misses
        private static readonly Dictionary<string, NutritionData> Loaded = new Dictionary<string, NutritionData>();

        public readonly Dictionary<string, float> Values = new Dictionary<string, float>();
        public float Priority = 0;
        public string[] Specialties;
        public readonly string Code;
        public EnumFoodCategory FoodCategory = EnumFoodCategory.Unknown;

        public NutritionData(JsonObject attributes) {
            Code = attributes["tag"].AsString();
            foreach (string name in Nutrients) {
                Values[name] = attributes[name].AsFloat();
            }
            Values.TrimExcess();
            if (attributes.KeyExists("priority")) {
                Priority = attributes["priority"].AsFloat();
            }
            if (attributes.KeyExists("specialties")) {
                Specialties = attributes["specialties"].AsArray<string>();
            }

            if (Values["sugar"] > 0.5) {
                FoodCategory = EnumFoodCategory.Fruit;
            }
            else if (Values["protein"] > 0.32) {
                FoodCategory = EnumFoodCategory.Protein;
            }
            else if (Values["starch"] > 0.32) {
                FoodCategory = EnumFoodCategory.Grain;
            }
            else if (Values["fiber"] > 0.14) {
                FoodCategory = EnumFoodCategory.Vegetable;
            }
            else if (Values["fat"] > 0.15) {
                FoodCategory = EnumFoodCategory.Dairy;
            }
        }

        public static void Load(IAsset asset) {
            JsonObject attributes = JsonObject.FromJson(asset.ToText());
            NutritionData data = new NutritionData(attributes);
            Loaded[data.Code] = data;
        }

        public static NutritionData Get(string tag) {
            NutritionData v = null;
            Loaded.TryGetValue(tag, out v);
            return v;
        }
    }
}
