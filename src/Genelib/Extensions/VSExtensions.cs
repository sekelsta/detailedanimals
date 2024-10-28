using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Genelib.Extensions {
    public static class VSExtensions {
        public static Entity GetEntityByUID(this IWorldAccessor world, long id) {
            Entity entityById = world.GetEntityById(id);
            if (entityById != null) {
                return entityById;
            }
            ICollection<Entity> loadedEntities = null;
            IServerWorldAccessor serverWorld = world as IServerWorldAccessor;
            if (serverWorld == null) {
                loadedEntities = (world as IClientWorldAccessor)?.LoadedEntities.Values;
            }
            else {
                loadedEntities = (serverWorld.LoadedEntities as CachingConcurrentDictionary<long, Entity>)?.Values;
            }
            if (loadedEntities == null) {
                return null;
            }
            foreach (Entity entity in loadedEntities) {
                if (entity.UniqueID() == id) {
                    return entity;
                }
            }
            return null;
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
