
using Vintagestory.GameContent;

namespace Genelib {
    public class NestContainer : InWorldContainer {
        public float PerishRate;

        public NestContainer(InventorySupplierDelegate inventorySupplier, string treeAttrKey) : base(inventorySupplier, treeAttrKey) { }

        public override float GetPerishRate() {
            return PerishRate;
        }
    }
}
