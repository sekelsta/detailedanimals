using System.Reflection;
using HarmonyLib;
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
                typeof(LooseItemFoodSource).GetMethod("ConsumeOnePortion", BindingFlags.Instance | BindingFlags.Public),
                prefix: new HarmonyMethod(typeof(AnimalFoodSourcePatches).GetMethod("LooseItem_ConsumeOnePortion_Prefix", BindingFlags.Static | BindingFlags.Public)) 
            );
            harmony.Patch(
                typeof(BlockEntityTrough).GetMethod("ConsumeOnePortion", BindingFlags.Instance | BindingFlags.Public),
                prefix: new HarmonyMethod(typeof(AnimalFoodSourcePatches).GetMethod("Trough_ConsumeOnePortion_Prefix", BindingFlags.Static | BindingFlags.Public)) 
            );
        }
    }
}

