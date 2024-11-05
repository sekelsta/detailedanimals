using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Genelib.Extensions {
    public static class VSExtensions {
        public static string GetDisplayName(this Entity entity) {
            string name = entity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
            if (name == null || name == "") {
                return entity.GetName();
            }
            return name;
        }

        public static bool IsMale(this Entity entity) {
            if (!entity.Properties.Attributes.KeyExists("male")) {
                JObject jo = (JObject) entity.Properties.Attributes.Token;
                jo.Add("male", !entity.Code.Path.Contains("-female"));
            }
            return entity.Properties.Attributes["male"].AsBool();
        }

        public static float WeightModifierExceptCondition(this Entity entity) {
            float weight = entity.WatchedAttributes.GetFloat("growthWeightFraction", 1);
            float dimorphism = entity.Properties.Attributes["weightDimorphism"].AsFloat(0);
            weight *= entity.IsMale() ? 1 + dimorphism : 1 - dimorphism;
            return weight;
        }

        public static float WeightModifier(this Entity entity) {
            float weight = WeightModifierExceptCondition(entity);
            weight *= entity.WatchedAttributes.GetFloat("animalWeight", 1);
            return weight;
        }

        public static float HealthyAdultWeightKg(this Entity entity) {
            float adultWeightKg = entity.Properties?.Attributes?["adultWeightKg"]?.AsFloat(160) ?? 160;
            float dimorphism = entity.Properties.Attributes["weightDimorphism"].AsFloat(0);
            float weight = entity.IsMale() ? 1 + dimorphism : 1 - dimorphism;
            return weight * adultWeightKg;
        }

        public static bool MatingAllowed(this Entity entity) {
            if (entity.WatchedAttributes.HasAttribute("domesticationstatus")) {
                if (!entity.WatchedAttributes.GetTreeAttribute("domesticationstatus").GetBool("multiplyAllowed", true)) {
                    return false;
                }
            }
            else if (entity.WatchedAttributes.GetBool("preventBreeding", false)) {
                return false;
            }
            return true;
        }

        public static string GetLangOptionallySuffixed(string key, string suffix) {
            if (Lang.HasTranslation(key)) {
                return Lang.Get(key);
            }
            return Lang.Get(key + suffix);
        }

        public static AnimationMetaData TryGetAnimation(this JsonObject json, string key) {
            return TryGetAnimation(json, key, key + "Speed");
        }

        public static AnimationMetaData TryGetAnimation(this JsonObject json, string key, string speedKey) {
            if (json[key].Exists) {
                string name = json[key].AsString()?.ToLowerInvariant();
                return new AnimationMetaData() {
                    Code = name,
                    Animation = name,
                    AnimationSpeed = json[speedKey].AsFloat(1f)
                }.Init();
            }
            return null;
        }
    }
}
