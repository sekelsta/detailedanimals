using System.Collections.Generic;
using System.Text;
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

        public static string TranslateTimeFromHours(ICoreAPI api, double hours) {
            double days = hours / api.World.Calendar.HoursPerDay;
            double months = days / api.World.Calendar.DaysPerMonth;
            int wholeYears = (int) (months / 12);
            int wholeMonths = (int) (months - wholeYears * 12);
            double daysCounted = (wholeYears * 12 + wholeMonths) * api.World.Calendar.DaysPerMonth;
            int wholeDays = (int) (days - daysCounted);
            int wholeHours = (int) (hours - (daysCounted + wholeDays) * api.World.Calendar.HoursPerDay);

            return TranslateTimeAmount(wholeYears, wholeMonths, wholeDays, wholeHours);
        }

        public static string TranslateTimeAmount(int years, int months, int days, int hours) {
            StringBuilder time = new StringBuilder("");
            if (years > 0) {
                string yearsKey = "detailedanimals:time-year" + years;
                time.Append((Lang.HasTranslation(yearsKey) ? Lang.Get(yearsKey) : Lang.Get("detailedanimals:time-year", years)) + " ");
            }
            if (months > 0) {
                string monthsKey = "detailedanimals:time-month" + months;
                time.Append((Lang.HasTranslation(monthsKey) ? Lang.Get(monthsKey) : Lang.Get("detailedanimals:time-month", months)) + " ");
            }
            if (years <= 0 && days > 0) {
                string daysKey = "detailedanimals:time-day" + days;
                time.Append((Lang.HasTranslation(daysKey) ? Lang.Get(daysKey) : Lang.Get("detailedanimals:time-day", days)) + " ");
            }
            if (years <= 0 && months <= 0 && hours > 0) {
                string hoursKey = "detailedanimals:time-hour" + hours;
                time.Append((Lang.HasTranslation(hoursKey) ? Lang.Get(hoursKey) : Lang.Get("detailedanimals:time-hour", hours)) + " ");
            }
            return time.ToString().TrimEnd();
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

        public static void CopyIfPresent(this TreeAttribute to, string key, TreeAttribute from) {
            IAttribute attribute = from.GetAttribute(key);
            if (attribute != null) {
                to.SetAttribute(key, attribute);
            }
        }
    }
}
