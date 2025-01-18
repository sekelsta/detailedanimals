using Genelib.Extensions;
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
        public const string Code = "genelib.harvestable";

        // Maximum values of each condition
        public const double SKIN_AND_BONES = 1 - 0.3;
        public const double MALNOURISHED = 1 - 0.18;
        public const double UNDERWEIGHT = 1 - 0.08;
        public const double LEAN = 1 - 0.036;
        public const double MODERATE = 1.036;
        public const double THICK = 1.08;
        public const double CHUBBY = 1.18;
        public const double FAT = 1.35;
        // No maximum for obese

        protected CreatureDropItemStack[] creatureDrops;

        protected AnimalHunger animalHunger;

        public DetailedHarvestable(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes) {
            JsonObject editedTypeAttributes = typeAttributes.Clone();
            editedTypeAttributes.Token["fixedweight"] = true;
            base.Initialize(properties, editedTypeAttributes);
            if (entity.World.Side == EnumAppSide.Server) {
                creatureDrops = typeAttributes["drops"].AsObject<CreatureDropItemStack[]>();
            }
        }

        public override void AfterInitialized(bool onFirstSpawn) {
            animalHunger = entity.GetBehavior<AnimalHunger>();
            if (onFirstSpawn) {
                animalHunger.BodyCondition = 0.9
                    + 0.07 * entity.World.Rand.NextDouble()
                    + 0.08 * entity.World.Rand.NextDouble();
                LastWeightUpdateTotalHours = entity.World.Calendar.TotalHours;
            }
            else {
                if (!entity.WatchedAttributes.HasAttribute("bodyCondition")) {
                    animalHunger.BodyCondition = (AnimalWeight + 3) / 4;
                }
            }
        }

        public override void OnEntityDeath(DamageSource damageSource) {
            if (entity.World.Side != EnumAppSide.Server) {
                return;
            }
            float healthyWeight = entity.WeightModifierExceptCondition();
            // Used by Butchering mod, also used by us now
            jsonDrops = new BlockDropItemStack[creatureDrops.Length];
            for (int i = 0; i < creatureDrops.Length; ++i) {
                creatureDrops[i].Resolve(entity.World, "genelib.Harvestable ", entity.Code);
                jsonDrops[i] = creatureDrops[i].WithAnimalWeight(AnimalWeight, healthyWeight);
            }
        }

        public override void OnGameTick(float deltaTime) {
            // Don't call base method. Don't reset AnimalWeight to 1.
        }

        public void GenerateDrops(IPlayer byPlayer, float dropQuantityMultiplier) {
            List<ItemStack> drops = new List<ItemStack>();
            float animalWeight = AnimalWeight;
            foreach (BlockDropItemStack drop in jsonDrops) {
                if (drop.Tool != null && (byPlayer == null || byPlayer.InventoryManager.ActiveTool != drop.Tool)) {
                    continue;
                }

                float multiplier = dropQuantityMultiplier * this.dropQuantityMultiplier;
                if (drop.DropModbyStat != null) {
                    multiplier *= byPlayer?.Entity?.Stats.GetBlended(drop.DropModbyStat) ?? 1;
                }
                multiplier *= animalWeight;

                ItemStack stack = drop.GetNextItemStack(multiplier);
                if (stack == null || stack.StackSize == 0) {
                    continue;
                }
                if (stack.Collectible is IResolvableCollectible irc) {
                    var slot = new DummySlot(stack);
                    irc.Resolve(slot, entity.World);
                    stack = slot.Itemstack;
                }
                while (stack.StackSize > stack.Collectible.MaxStackSize ) {
                    ItemStack overflow = stack.GetEmptyClone();
                    overflow.StackSize = stack.Collectible.MaxStackSize;
                    stack.StackSize -= stack.Collectible.MaxStackSize;
                    drops.Add(overflow);
                }
                drops.Add(stack);
                if (drop.LastDrop) {
                    break;
                }
            }

            foreach (ItemStack stack in entity.GetDrops(entity.World, entity.Pos.AsBlockPos, byPlayer)) {
                drops.Add(stack);
            }

            // TODO: Make sure the player gets equipped items back

            inv.AddSlots(drops.Count - inv.Count);
            for (int i = 0; i < drops.Count; ++i) {
                inv[i].Itemstack = drops[i];
            }

            TreeAttribute tree = new TreeAttribute();
            inv.ToTreeAttributes(tree);
            entity.WatchedAttributes["harvestableInv"] = tree;
            entity.WatchedAttributes.MarkPathDirty("harvestableInv");

            if (entity.World.Side == EnumAppSide.Server) {
                entity.World.BlockAccessor.GetChunkAtBlockPos(entity.Pos.AsBlockPos).MarkModified();
            }
        }

        public static bool generateDrops_Prefix(EntityBehaviorHarvestable __instance, IPlayer byPlayer, float dropQuantityMultiplier) {
            if (!(__instance is DetailedHarvestable)) {
                return true;
            }
            DetailedHarvestable instance = (DetailedHarvestable)__instance;
            instance.GenerateDrops(byPlayer, dropQuantityMultiplier);

            return false;
        }

        public override void GetInfoText(StringBuilder infotext) {
            base.GetInfoText(infotext);

            double[] conditionBoundaries = new double[] {SKIN_AND_BONES, MALNOURISHED, UNDERWEIGHT, LEAN, MODERATE, THICK, CHUBBY, FAT};
            int bodyScore = 1;
            foreach (double b in conditionBoundaries) {
                if (animalHunger.BodyCondition > b) {
                    bodyScore += 1;
                }
                else {
                    break;
                }
            }
            Debug.Assert(bodyScore >= 1);
            Debug.Assert(bodyScore <= 9);

            double weightKilograms = entity.Properties.Attributes["adultWeightKg"].AsFloat();
            weightKilograms *= entity.WeightModifier();
            double weightPounds = weightKilograms * 2.20462;

            string unitsSuffix = GenelibSystem.Config.WeightSuffix();
            string conditionKey = "genelib:infotext-bodycondition" + bodyScore.ToString();
            string genderSuffix = entity.IsMale() ? "-male" : "-female";
            string text = Lang.GetUnformatted("genelib:infotext-conditionweight" + unitsSuffix)
                .Replace("{condition}", VSExtensions.GetLangOptionallySuffixed(conditionKey, genderSuffix))
                .Replace("{pounds}", roundNicely(weightPounds))
                .Replace("{kilograms}", roundNicely(weightKilograms));
            if (entity.Alive) {
                double d = animalHunger.WeightShiftAmount();
                if (d > 0.01) {
                    if (animalHunger.BodyCondition > 1.06) {
                        text += " <font color=\"#ee6933\">↑</font>";
                    }
                    else if (animalHunger.BodyCondition <= UNDERWEIGHT) {
                        text += " <font color=\"#65f68e\">↑</font>";
                    }
                    else {
                        text += " ↑";
                    }
                }
                else if (d < -0.01) {
                    if (animalHunger.BodyCondition < 0.94) {
                        text += " <font color=\"#ee6933\">↓</font>";
                    }
                    else if (animalHunger.BodyCondition >= THICK) {
                        text += " <font color=\"#65f68e\">↓</font>";
                    }
                    else {
                        text += " ↓";
                    }
                }
            }
            infotext.AppendLine(text);
        }

        private static string roundNicely(double x) {
            if (x == 0) {
                return x.ToString();
            }
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
