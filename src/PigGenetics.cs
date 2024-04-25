
using Vintagestory.API.Common.Entities;
using Genelib;

namespace VintageInheritance {
    static class PigGenetics {
        public static void Finalize(Genome genome, Entity entity) {
            // TODO: If genome contains two copies of lethal white, set one to wildtype
        }

        public static void Interpret(Genome genome, Entity entity) {
            // TODO: Set entity.WatchedAttributes["textureIndex"] based on genes
        }
    }
}
