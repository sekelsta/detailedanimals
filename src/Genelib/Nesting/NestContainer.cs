
using Vintagestory.GameContent;

namespace Genelib {
    public class NestContainer : InWorldContainer {
        public NestContainer(InventorySupplierDelegate inventorySupplier, string treeAttrKey) : base(inventorySupplier, treeAttrKey) { }

        public override float GetPerishRate() {
            return 1f;
        }
    }
}
