using System;
using System.Collections.Generic;
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
        public const string Code = GeneticsModSystem.NamePrefix + "harvestable";
        public static readonly float MinReproductionWeight = 0.85f;
        public static readonly float MaxReproductionWeight = 1.5f;

        protected CreatureDropItemStack[] creatureDrops;

        public DetailedHarvestable(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes) {
            JsonObject editedTypeAttributes = typeAttributes.Clone();
            editedTypeAttributes.Token["fixedweight"] = true;
            base.Initialize(properties, editedTypeAttributes);
            jsonDrops = null; // Needlessly initialized by base method
            if (entity.World.Side == EnumAppSide.Server) {
                creatureDrops = typeAttributes["drops"].AsObject<CreatureDropItemStack[]>();
            }
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

        public static bool generateDrops_Prefix(EntityBehaviorHarvestable __instance, IPlayer byPlayer, float dropQuantityMultiplier) {
            if (!(__instance is DetailedHarvestable)) {
                return true;
            }
            DetailedHarvestable instance = (DetailedHarvestable)__instance;
            Entity entity = instance.entity;
            float cappedAnimalWeight = Math.Min(instance.AnimalWeight, 1.08f);
            float growth = instance.entity.WatchedAttributes.GetFloat("growthWeightFraction", 1);
            List<ItemStack> drops = new List<ItemStack>();
            foreach (CreatureDropItemStack drop in instance.creatureDrops) {
                if (drop.Tool != null && (byPlayer == null || byPlayer.InventoryManager.ActiveTool != drop.Tool)) {
                    continue;
                }
                drop.Resolve(entity.World, "genelib.Harvestable ", entity.Code);

                float multiplier = dropQuantityMultiplier * instance.dropQuantityMultiplier;
                if (drop.DropModbyStat != null) {
                    multiplier *= byPlayer?.Entity?.Stats.GetBlended(drop.DropModbyStat) ?? 1;
                }
                if (drop.Category == EnumDropCategory.Meat) {
                    multiplier *= growth * cappedAnimalWeight;
                }
                else if (drop.Category == EnumDropCategory.Pelt) {
                    multiplier *= (float)Math.Pow(growth, 0.6667f);
                }
                else if (drop.Category == EnumDropCategory.Fat) {
                    float fatness = Math.Max(0, cappedAnimalWeight - 0.8f) / 0.2f;
                    multiplier *= growth * fatness * fatness;
                }

                ItemStack stack = drop.GetNextItemStack(multiplier);
                if (stack == null || stack.StackSize == 0) {
                    continue;
                }
                if (stack.Collectible is IResolvableCollectible irc) {
                    var slot = new DummySlot(stack);
                    irc.Resolve(slot, entity.World);
                    stack = slot.Itemstack;
                }
                drops.Add(stack);
                if (drop.LastDrop) {
                    break;
                }
            }

            var eagent = entity as EntityAgent;
            if (eagent.GearInventory != null) {
                foreach (var slot in eagent.GearInventory) {
                    if (!slot.Empty) {
                        drops.Add(slot.Itemstack);
                    }
                }
            }

            instance.inv.AddSlots(drops.Count - instance.inv.Count);
            for (int i = 0; i < drops.Count; ++i) {
                instance.inv[i].Itemstack = drops[i];
            }

            TreeAttribute tree = new TreeAttribute();
            instance.inv.ToTreeAttributes(tree);
            entity.WatchedAttributes["harvestableInv"] = tree;
            entity.WatchedAttributes.MarkPathDirty("harvestableInv");
            entity.WatchedAttributes.MarkPathDirty("harvested");

            if (instance.entity.World.Side == EnumAppSide.Server) {
                entity.World.BlockAccessor.GetChunkAtBlockPos(entity.ServerPos.AsBlockPos).MarkModified();
            }

            return false;
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
            baseWeightKg *= entity.WatchedAttributes.GetFloat("growthWeightFraction", 1);
            double weightKilograms = AnimalWeight * baseWeightKg;
            double weightPounds = weightKilograms * 2.20462;

            string unitsSuffix = GeneticsModSystem.Config.WeightSuffix();
            string conditionKey = "genelib:infotext-bodycondition" + bodyScore.ToString();
            string genderSuffix = entity.IsMale() ? "-male" : "-female";
            string text = Lang.GetUnformatted("genelib:infotext-conditionweight" + unitsSuffix)
                .Replace("{condition}", VSExtensions.GetLangOptionallySuffixed(conditionKey, genderSuffix))
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
