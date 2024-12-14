using System;
using System.Reflection;
using HarmonyLib;
using Genelib.Extensions;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Genelib {
    public class HarmonyPatches {
        private static Harmony harmony = new Harmony("sekelsta.genelib");

        public static void Patch() {
            harmony.Patch(
                typeof(EntityBehaviorHarvestable).GetMethod("generateDrops", BindingFlags.Instance | BindingFlags.NonPublic),
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
                postfix: new HarmonyMethod(typeof(HarmonyPatches).GetMethod("UpdateMaxHealth_Postfix", BindingFlags.Static | BindingFlags.Public)) 
            );
        }

        public static void UpdateMaxHealth_Postfix(EntityBehaviorHealth __instance) {
            Entity entity = __instance.entity;
            if (entity.Api.Side != EnumAppSide.Server) {
                return;
            }
            // Note this is called for everything with a health behavior, players, animals, monsters
            float multiplier = (float)Math.Sqrt(entity.WeightModifierExceptCondition());
            if (multiplier > 0.999 && multiplier < 1.001) {
                return;
            }

            // Repeating the vanilla calculation because it seems when I read MaxHealth right after it's not always set to the result
            float totalMaxHealth = __instance.BaseMaxHealth;
            foreach (var val in __instance.MaxHealthModifiers) {
                totalMaxHealth += val.Value;
            }
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
        }
    }
}

