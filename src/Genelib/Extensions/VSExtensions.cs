using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Genelib.Extensions {
    public static class VSExtensions {
        public static long UniqueID(this Entity entity) {
            return entity.WatchedAttributes.GetLong("UID", entity.EntityId);
        }

        public static string GetDisplayName(this Entity entity) {
            string name = entity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
            if (name == null || name == "") {
                return entity.GetName();
            }
            return name;
        }

        public static bool IsMale(this Entity entity) {
            // Sometimes property attributes are null, so need to check
            if (entity.Properties.Attributes == null) {
                return false;
            }
            if (!entity.Properties.Attributes.KeyExists("male")) {
                JObject jo = (JObject) entity.Properties.Attributes.Token;
                jo.Add("male", !entity.Code.Path.Contains("-female"));
            }
            return entity.Properties.Attributes["male"].AsBool();
        }

        public static float WeightModifierExceptCondition(this Entity entity) {
            float weight = entity.WatchedAttributes.GetFloat("growthWeightFraction", 1);
            float dimorphism = entity.Properties.Attributes?["weightDimorphism"].AsFloat(0) ?? 0;
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
            float dimorphism = entity.Properties.Attributes?["weightDimorphism"].AsFloat(0) ?? 0;
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

        public static bool IsCloseRelative(this Entity entity, Entity other) {
            long ourID = entity.UniqueID();
            long theirID = other.UniqueID();
            long ourMother = entity.WatchedAttributes.GetLong("motherId", -1);
            long ourFather = entity.WatchedAttributes.GetLong("fatherId", -1);
            long ourFoster = entity.WatchedAttributes.GetLong("fosterId", -1);
            long theirMother = other.WatchedAttributes.GetLong("motherId", -1);
            long theirFather = other.WatchedAttributes.GetLong("fatherId", -1);
            long theirFoster = other.WatchedAttributes.GetLong("fosterId", -1);

            bool isParent = ourID == theirMother || ourID == theirFather || ourID == theirFoster;
            bool isChild = theirID == ourMother || theirID == ourFather || theirID == ourFoster;
            // Skip sibling check for adoption
            bool sharesMother = ourMother != -1 && (ourMother == theirMother || ourMother == theirFather);
            bool sharesFather = ourFather != -1 && (ourFather == theirFather || ourFather == theirMother);
            return isParent || isChild || sharesMother || sharesFather;
        }

        public static bool OwnedBy(this Entity entity, IPlayer player) {
            return player != null && entity.WatchedAttributes.GetTreeAttribute("ownedby")?.GetString("uid") == player.PlayerUID;
        }

        public static bool OwnedByOther(this Entity entity, IPlayer player) {
            string ownerUID = entity.WatchedAttributes.GetTreeAttribute("ownedby")?.GetString("uid");
            return ownerUID != null && ownerUID != player?.PlayerUID;
        }

        public static GenomeType GetGenomeType(this EntityProperties entityType) {
            JsonObject[] jsonBehaviors = entityType.Server.BehaviorsAsJsonObj;
            for (int i = 0; i < jsonBehaviors.Length; ++i) {
                string code = jsonBehaviors[i]["code"].AsString();
                if (code == EntityBehaviorGenetics.Code) {
                    return GenomeType.Get(
                        AssetLocation.Create(jsonBehaviors[i]["genomeType"].AsString(), entityType.Code.Domain)
                    );
                } 
            }
            return null;
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
