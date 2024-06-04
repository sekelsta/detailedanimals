using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Genelib {
    public class DetailedHarvestable : EntityBehaviorHarvestable {
        public const string Code = "detailedharvestable";
        public static readonly float MinReproductionWeight = 0.85f;
        public static readonly float MaxReproductionWeight = 1.5f;

        public DetailedHarvestable(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes) {
            JsonObject editedTypeAttributes = typeAttributes.Clone();
            editedTypeAttributes.Token["fixedweight"] = true;
            base.Initialize(properties, editedTypeAttributes);
        }

        public override void AfterInitialized(bool onFirstSpawn) {
            if (onFirstSpawn) {
                AnimalWeight = 0.88f
                    + 0.07f * (float)entity.World.Rand.NextDouble()
                    + 0.08f * (float)entity.World.Rand.NextDouble();
                LastWeightUpdateTotalHours = entity.World.Calendar.TotalHours;
            }
        }

        public override void OnGameTick(float deltaTime) {
            // Don't call base method. Don't reset AnimalWeight to 1.
        }

        public override void GetInfoText(StringBuilder infotext) {
            base.GetInfoText(infotext);
            double[] conditionBoundaries = new double[] {-0.35, -0.18, -0.08, -0.036, 0.036, 0.08, 0.18, 0.35};
            int bodyScore = 1;
            foreach (double b in conditionBoundaries) {
                if (AnimalWeight > 1 + b) {
                    bodyScore += 1;
                }
                else {
                    break;
                }
            }
            Debug.Assert(bodyScore >= 1);
            Debug.Assert(bodyScore <= 9);

            float baseWeightKg = entity.Properties.Attributes["adultWeightKg"].AsFloat();
            float debugval = baseWeightKg;
            baseWeightKg *= entity.WatchedAttributes.GetFloat("growthWeightFraction", 1);
            double weightKilograms = AnimalWeight * baseWeightKg;
            double weightPounds = weightKilograms * 2.20462;

            string unitsSuffix = GeneticsModSystem.Config.WeightSuffix();
            string conditionKey = "genelib:infotext-bodycondition" + bodyScore.ToString() + (entity.IsMale() ? "-male" : "-female");
            string text = Lang.GetUnformatted("genelib:infotext-conditionweight" + unitsSuffix)
                .Replace("{condition}", Lang.Get(conditionKey))
                .Replace("{pounds}", roundNicely(weightPounds))
                .Replace("{kilograms}", roundNicely(weightKilograms));
            infotext.AppendLine(text);
        }

        private string roundNicely(double x) {
            double l = Math.Floor(Math.Log10(Math.Abs(x))) - 2;
            double r = Math.Pow(10.0, l);
            if (x / r > 500) {
                r *= 5;
            }
            else if (x / r > 200) {
                r *= 2;
            }
            double rounded = r * Math.Round(x / r);
            return ((float)rounded).ToString();
        }

        public override string PropertyName() => Code;
    }
}
