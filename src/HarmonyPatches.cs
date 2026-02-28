using System;
using System.Collections.Generic;
using System.Reflection;
using Cairo;
using HarmonyLib;
using Genelib.Extensions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using DetailedAnimals.Extensions;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.ServerMods;

namespace DetailedAnimals {
    public class HarmonyPatches {
        private static Harmony? harmony;

        public static void Patch() {
            if (harmony != null) return;
            harmony = new Harmony("sekelsta.detailedanimals");
            
            harmony.Patch(
                typeof(EntityBehaviorHarvestable).GetMethod("GenerateDrops", BindingFlags.Instance | BindingFlags.Public),
                prefix: new HarmonyMethod(typeof(DetailedHarvestable).GetMethod("generateDrops_Prefix", BindingFlags.Static | BindingFlags.Public)) 
            );
            harmony.Patch(
                typeof(LooseItemFoodSource).GetMethod("IsSuitableFor", BindingFlags.Instance | BindingFlags.Public),
                prefix: new HarmonyMethod(typeof(AnimalFoodSourcePatches).GetMethod("LooseItem_IsSuitableFor_Prefix", BindingFlags.Static | BindingFlags.Public)) 
            );
            harmony.Patch(
                typeof(LooseItemFoodSource).GetMethod("ConsumeOnePortion", BindingFlags.Instance | BindingFlags.Public),
                prefix: new HarmonyMethod(typeof(AnimalFoodSourcePatches).GetMethod("LooseItem_ConsumeOnePortion_Prefix", BindingFlags.Static | BindingFlags.Public)) 
            );
            harmony.Patch(
                typeof(BlockEntityTrough).GetMethod("IsSuitableFor", BindingFlags.Instance | BindingFlags.Public),
                prefix: new HarmonyMethod(typeof(AnimalFoodSourcePatches).GetMethod("Trough_IsSuitableFor_Prefix", BindingFlags.Static | BindingFlags.Public)) 
            );
            harmony.Patch(
                typeof(BlockEntityTrough).GetMethod("ConsumeOnePortion", BindingFlags.Instance | BindingFlags.Public),
                prefix: new HarmonyMethod(typeof(AnimalFoodSourcePatches).GetMethod("Trough_ConsumeOnePortion_Prefix", BindingFlags.Static | BindingFlags.Public)) 
            );
            harmony.Patch(
                typeof(EntityBehaviorHealth).GetMethod("UpdateMaxHealth", BindingFlags.Instance | BindingFlags.Public),
                prefix: new HarmonyMethod(typeof(HarmonyPatches).GetMethod("UpdateMaxHealth_Prefix", BindingFlags.Static | BindingFlags.Public)) 
            );
            harmony.Patch(
                typeof(EntitySidedProperties).GetConstructor(BindingFlags.Instance | BindingFlags.Public, new[] { typeof(JsonObject[]), typeof(Dictionary<string, JsonObject>)}),
                prefix: new HarmonyMethod(typeof(HarmonyPatches).GetMethod("EntitySidedProperties_Ctor_Prefix", BindingFlags.Static | BindingFlags.Public)) 
            );
            harmony.Patch(
                typeof(AiTaskBaseTargetable).GetMethod("GetOwnGeneration", BindingFlags.Instance | BindingFlags.Public),
                postfix: new HarmonyMethod(typeof(HarmonyPatches).GetMethod("AiTaskBaseTargetable_GetOwnGeneration_Postfix", BindingFlags.Static | BindingFlags.Public)) 
            );
            harmony.Patch(
                typeof(CollectibleBehaviorHandbookTextAndExtraInfo).GetMethod("addIngredientForInfo", BindingFlags.Instance | BindingFlags.NonPublic),
                postfix: new HarmonyMethod(typeof(HarmonyPatches).GetMethod("addIngredientForInfo_Postfix", BindingFlags.Static | BindingFlags.Public)) 
            );
        }

        public static bool UpdateMaxHealth_Prefix(EntityBehaviorHealth __instance) {
            Entity entity = __instance.entity;
            if (entity.Api.Side != EnumAppSide.Server) {
                return true;
            }
            // Note this is called for everything with a health behavior, players, animals, monsters
            float weight = entity.BaseWeight();
            if (weight > 0.999 && weight < 1.001) {
                return true;
            }
            float multiplier = (float)Math.Sqrt(weight);

            #pragma warning disable CS0618
            // Repeating the vanilla calculation because it seems when I read MaxHealth right after it's not always set to the result
            float totalMaxHealth = __instance.BaseMaxHealth;
            if (__instance.MaxHealthModifiers != null) {
                foreach (var val in __instance.MaxHealthModifiers) {
                    totalMaxHealth += val.Value;
                }
            }
            #pragma warning restore CS0618
            totalMaxHealth += entity.Stats.GetBlended("maxhealthExtraPoints") - 1;

            bool wasFullHealth = __instance.Health >= __instance.MaxHealth;
            float newHealth = totalMaxHealth * multiplier;
            if (newHealth < 100) {
                newHealth = ((int)Math.Max(1, 10 * newHealth)) / 10f;
            }
            else {
                newHealth = (int)newHealth;
            }
            __instance.MaxHealth = newHealth;
            if (wasFullHealth) {
                __instance.Health = __instance.MaxHealth;
            }
            return false;
        }

        public static bool EntitySidedProperties_Ctor_Prefix(EntitySidedProperties __instance, ref JsonObject[] behaviors, ref Dictionary<string, JsonObject> commonConfigs) {
            if (commonConfigs == null || !commonConfigs.ContainsKey(AnimalHunger.Code)) {
                return true;
            }

            int hungerIndex = -1;
            int harvestableIndex = -1;
            int multiplyIndex = -1;
            for (int i = 0; i < behaviors.Length; ++i) {
                string code = behaviors[i]["code"].AsString();
                if (code == "harvestable") {
                    harvestableIndex = i;
                }
                else if (code == "multiply" || code == "genelib.multiply") {
                    multiplyIndex = i;
                }
                else if (code == AnimalHunger.Code) {
                    hungerIndex = i;
                }
            }

            if (harvestableIndex != -1) {
                JObject harvestableJson = (JObject)(behaviors[harvestableIndex].Token);
                harvestableJson.Property("code").Value = new JValue(DetailedHarvestable.Code);
            }

            if (multiplyIndex != -1) {
                JObject multiplyJson = (JObject)(behaviors[multiplyIndex].Token);
                multiplyJson.Property("code").Value = new JValue(Reproduce.Code);
            }

            if (commonConfigs.ContainsKey("genelib.multiply")) {
                commonConfigs[Reproduce.Code] = commonConfigs["genelib.multiply"];
            }
            else if (commonConfigs.ContainsKey("multiply") && multiplyIndex != -1) {
                // Don't copy over vanilla multiply config if the animal is already set up to use reproduce
                commonConfigs[Reproduce.Code] = commonConfigs["multiply"];
            }

            if (hungerIndex == -1) {
                JsonObject[] oldBehaviors = behaviors;
                behaviors = new JsonObject[oldBehaviors.Length + 1];
                Array.Copy(oldBehaviors, behaviors, oldBehaviors.Length);
                behaviors[behaviors.Length - 1] = JsonObject.FromJson("{code: \"" + AnimalHunger.Code + "\"}");
            }

            commonConfigs.Remove("harvestable", out JsonObject harvestable);
            if (harvestable != null) {
                commonConfigs.Add(DetailedHarvestable.Code, harvestable);
            }

            return true;
        }

        public static void AiTaskBaseTargetable_GetOwnGeneration_Postfix(AiTaskBaseTargetable __instance, ref int __result) {
            double tamingProgress = __instance.entity.GetBehavior<BehaviorAge>()?.TamingProgress ?? 0;
            __result += (int)Math.Floor(10 * tamingProgress);
        }
        
        public static void addIngredientForInfo_Postfix(ref bool __result, ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components) {
            __result = addNutritionInfo(capi, openDetailPageFor, stack , components, __result);
        }

        static bool addNutritionInfo(ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, bool haveText) {
            FoodNutritionProperties nutriProps = stack.Collectible.GetNutritionProperties(capi.World, stack, null);
            NutritionData data = AnimalHunger.GetNutritionData(stack, nutriProps);

            if (data?.Values == null) return haveText;

            int precision = 20;
            
            Dictionary<string, int> values = [];
            double leftMargin = 0;
            foreach ((string type, float value) in data.Values)
            {
                if (type is "water" or "minerals") continue;
                
                int numTicks = Math.Min(precision, (int)Math.Round(value * precision, MidpointRounding.AwayFromZero));
                if (numTicks <= 0) continue;
                
                string output = "• " + Lang.GetUnformatted("detailedanimals:gui-animalinfo-nutrient-" + type).Replace("{n}", "");
                leftMargin = Math.Max(leftMargin, CairoFont.WhiteSmallText().GetTextExtents(output).Width);
                values.Add(output, numTicks);
            }
            
            if (values.Count <= 0) return haveText;
            leftMargin += 10;
            
            CollectibleBehaviorHandbookTextAndExtraInfo.AddHeading(components, capi, "detailedanimals:handbook-nutritiontitle", ref haveText);
            components.Add(new ClearFloatTextComponent(capi, 2));

            foreach ((string name, int numTicks) in values)
            {
                string ticks = "";
                for (int i = 0; i < numTicks; i++) ticks += "█";

                components.Add(new RichTextComponent(capi, name, CairoFont.WhiteSmallText()) { PaddingLeft = 2, PaddingRight = CairoFont.WhiteSmallText().GetTextExtents(name).Width * -1 });
                components.Add(new RichTextComponent(capi, ticks, CairoFont.WhiteSmallText()) { PaddingLeft = leftMargin });
                components.Add(new ClearFloatTextComponent(capi, 4));
            }

            return haveText;
        }
    }
}

