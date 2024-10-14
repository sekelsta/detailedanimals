
using Vintagestory.GameContent;

namespace Genelib {
    public class NestContainer : InWorldContainer {
        public NestContainer(InventorySupplierDelegate inventorySupplier, string treeAttrKey) : base(inventorySupplier, treeAttrKey) { }

        public override float GetPerishRate() {
            // Scale with month length, just like incubation time does
            return 9 / Api.World.Calendar.DaysPerMonth;
        }
    }
}
