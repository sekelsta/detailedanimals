using Newtonsoft.Json.Linq;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace Genelib.Extensions {
    public static class VSExtensions {
        public static bool IsMale(this Entity entity) {
            if (!entity.Properties.Attributes.KeyExists("male")) {
                JObject jo = (JObject) entity.Properties.Attributes.Token;
                jo.Add("male", !entity.Code.Path.Contains("-female"));
            }
            return entity.Properties.Attributes["male"].AsBool();
        }

        public static float WeightModifier(this Entity entity) {
            float weight = entity.WatchedAttributes.GetFloat("growthWeightFraction", 1);
            weight *= entity.WatchedAttributes.GetFloat("animalWeight", 1);
            float dimorphism = entity.Properties.Attributes["weightDimorphism"].AsFloat(0);
            weight *= entity.IsMale() ? 1 + dimorphism : 1 - dimorphism;
            return weight;
        }

        public static string GetLangOptionallySuffixed(string key, string suffix) {
            if (Lang.HasTranslation(key)) {
                return Lang.Get(key);
            }
            return Lang.Get(key + suffix);
        }
    }
}
