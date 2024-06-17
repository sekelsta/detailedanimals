using Newtonsoft.Json.Linq;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace Genelib {
    public static class VSExtensions {
        public static bool IsMale(this Entity entity) {
            if (!entity.Properties.Attributes.KeyExists("male")) {
                JObject jo = (JObject) entity.Properties.Attributes.Token;
                jo.Add("male", !entity.Code.Path.Contains("-female"));
            }
            return entity.Properties.Attributes["male"].AsBool();
        }

        public static string GetLangOptionallySuffixed(string key, string suffix) {
            if (Lang.HasTranslation(key)) {
                return Lang.Get(key);
            }
            return Lang.Get(key + suffix);
        }
    }
}
