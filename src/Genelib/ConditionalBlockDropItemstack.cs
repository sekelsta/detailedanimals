using Vintagestory.API.Common;

namespace Genelib {
    public class ConditionalBlockDropItemStack : BlockDropItemStack {
        public string when;
        public string whennot;
        public string coefficient;
    }
}
