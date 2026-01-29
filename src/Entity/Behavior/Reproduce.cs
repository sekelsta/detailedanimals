using DetailedAnimals.Extensions;
using Genelib;
using Genelib.Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace DetailedAnimals {
    public class Reproduce : GeneticMultiply {
        public new const string Code = "reproduce";

        public virtual double ExtraGrowthTarget {
            get => 0;
        }

        public virtual double ExtraGrowthTargetHour {
            get => 0;
        }

        public double GrowthPausedSince {
            get => entity.WatchedAttributes.GetTreeAttribute("grow")?.GetDouble("growthPausedSince", -1) ?? entity.World.Calendar.TotalHours;
        }

        public int NextGeneration() {
            int generation = entity.WatchedAttributes.GetInt("generation", 0);
            if (entity.WatchedAttributes.GetBool("fedByPlayer", false)) {
                return generation + 1;
            }
            return generation;
        }

        public Reproduce(Entity entity) : base(entity) { }

        // Pop as in stack push/pop
        public TreeAttribute PopChild() {
            TreeArrayAttribute litter = Litter;
            if (litter == null) {
                return null;
            }
            TreeAttribute child = litter.value[0];
            if (litter.value.Length <= 1) {
                SetNotPregnant();
            }
            else {
                litter.value = litter.value[1..^0];
                entity.WatchedAttributes.MarkPathDirty("multiply");
            }
            return child;
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes) {
            base.Initialize(properties, attributes);
            TryGetPregnantChance = 0.2;
            PortionsEatenForMultiply = 0;
            MatingFoodCost = 0;

            double minAgeDays = GenelibConfig.AnimalMonthsToGameDays(attributes["minAgeMonths"].AsFloat(0));
            double birthDate = entity.WatchedAttributes.GetDouble("birthTotalDays", 0);
            TotalDaysCooldownUntil = Math.Max(TotalDaysCooldownUntil, birthDate + minAgeDays);
        }

        public override bool ShouldEat { get => true; }

        public override bool EntityCanMate(Entity entity) {
            if (!base.EntityCanMate(entity)) {
                return false;
            }
            bool farmed = entity.WatchedAttributes.GetBool("fedByPlayer") || entity.WatchedAttributes.GetDouble("fedByPlayerTotalSatiety") > 0;
            return farmed || (AnimalConfig.Instance.WildBreeding && (entity.GetBehavior<AnimalHunger>()?.Fullness ?? 1) > AnimalHunger.HUNGRY);
        }

        // If the animal dies, you lose the pregnancy even if you later revive it
        public override void OnEntityDeath(DamageSource damageSource) {
            SetNotPregnant();
        }

        // And on revival, if not pregnant, you do not progress towards becoming so
        public override void OnEntityRevive() {
            TotalDaysCooldownUntil += (entity.World.Calendar.TotalHours - GrowthPausedSince) / 24.0;
        }

        public override bool EntityHasEatenEnoughToMate(Entity entity) {
            double animalWeight = entity.BodyCondition();
            if (animalWeight <= DetailedHarvestable.MALNOURISHED || animalWeight > DetailedHarvestable.FAT) {
                return false;
            }
            return true;
        }

        protected override void GetReadinessInfoText(StringBuilder infotext, double animalWeight) {
            if (!entity.MatingAllowed()) {
                return;
            }
            if (animalWeight <= DetailedHarvestable.MALNOURISHED) {
                infotext.AppendLine(Lang.Get("detailedanimals:infotext-reproduce-underweight"));
                return;
            }
            else if (animalWeight > DetailedHarvestable.FAT) {
                infotext.AppendLine(Lang.Get("detailedanimals:infotext-reproduce-overweight"));
                return;
            }

            base.GetReadinessInfoText(infotext, animalWeight);
        }

        public override string PropertyName() => Code;
    }
}
