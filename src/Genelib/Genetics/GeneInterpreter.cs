using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Genelib {
    public interface GeneInterpreter {
        string Name {
            get;
        }
        // Called on first spawn after genome is generated, but before it is set. Intended to modify the genome so
        // that the genes are suitable for a wild-spawned adult, such as by ensuring lethal alleles are not homozygous.
        // Not called when the gnome was created by reproduction
        void Finalize(Genome genome, AlleleFrequencies frequencies, Random random) {
            // Do nothing
        }

        // Called during pregnancy. If you override this, you should aso override Finalize(). Mother and offspring
        // should normally use the same GeneInterpreter, but if they are different, the mother's will be the one
        // called here.
        bool EmbryonicLethal(Genome genome) {
            return false;
        }

        // Called on first spawn or when an entity is born, after genome is finalized
        void Interpret(EntityBehaviorGenetics genetics);

        ITexPositionSource GetTextureSource(EntityBehaviorGenetics genetics, ref EnumHandling handling) {
            return null;
        }
    }
}
