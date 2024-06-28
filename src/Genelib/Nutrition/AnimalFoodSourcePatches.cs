
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace Genelib {
    public class AnimalFoodSourcePatches {
        public static bool LooseItem_ConsumeOnePortion_Prefix(LooseItemFoodSource __instance, Entity entity, float __result) {
            AnimalHunger hunger = entity.GetBehavior<AnimalHunger>();
            if (hunger == null) {
                return true;
            }
            EntityItem looseitem = (EntityItem)__instance.GetType().GetField("entity").GetValue(__instance);
            hunger.Eat(looseitem.Itemstack);
            if (looseitem.Itemstack.StackSize <= 0) {
                looseitem.Die();
            }
            __result = 0;
            return false;
        }

        public static bool Trough_ConsumeOnePortion_Prefix(BlockEntityTrough __instance, Entity entity, float __result) {
            AnimalHunger hunger = entity.GetBehavior<AnimalHunger>();
            if (hunger == null) {
                return true;
            }
            __result = 0;
            FieldInfo contentCodeField = __instance.GetType().GetField("contentCode", BindingFlags.Instance | BindingFlags.NonPublic);
            string contentCode = (string)contentCodeField.GetValue(__instance);
            ContentConfig config = __instance.contentConfigs.FirstOrDefault(c => c.Code == contentCode);
            if (config == null) {
                return false;
            }
            for (int i = 0; i < config.QuantityPerFillLevel && !__instance.Inventory.Empty; ++i) {
                hunger.Eat(__instance.Inventory[0]);
            }
            if (__instance.Inventory[0].Empty) {
                contentCodeField.SetValue(__instance, "");
            }
            __instance.Inventory[0].MarkDirty();
            __instance.MarkDirty();
            return false;
        }
    }
}
