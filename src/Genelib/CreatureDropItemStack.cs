using Vintagestory.API.Common;

namespace Genelib {
    public enum EnumDropCategory {
        Other = 0,
        Meat = 1,
        Pelt = 2,
        Fat = 3
    }
    public class CreatureDropItemStack : BlockDropItemStack {
        public EnumDropCategory Category = EnumDropCategory.Other;
    }
}
