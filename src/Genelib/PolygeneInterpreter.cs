using System;
using Genelib.Extensions;
using Vintagestory.API.Common.Entities;

namespace Genelib {
    public class PolygeneInterpreter : GeneInterpreter {
        private static readonly int NUM_DIVERSITY_GENES = 32;
        private static readonly int NUM_FERTILITY_GENES = 32;
        internal static readonly int NUM_POLYGENES = NUM_DIVERSITY_GENES + NUM_FERTILITY_GENES;

        void GeneInterpreter.Finalize(Genome genome, AlleleFrequencies frequencies, Random random) {
            for (int i = 2 * NUM_DIVERSITY_GENES; i < 2 * (NUM_DIVERSITY_GENES + NUM_FERTILITY_GENES); ++i) {
                if (random.NextBool()) {
                    genome.anonymous[i] = 0;
                }
            }
            for (int i = NUM_DIVERSITY_GENES; i < NUM_DIVERSITY_GENES + NUM_FERTILITY_GENES; ++i) {
                if (genome.anonymous[2 * i] == genome.anonymous[2 * i + 1]) {
                    genome.anonymous[2 * i] = 0;
                }
            }
        }

        bool GeneInterpreter.EmbryonicLethal(Genome genome) {
            for (int i = NUM_DIVERSITY_GENES; i < NUM_DIVERSITY_GENES + NUM_FERTILITY_GENES; ++i) {
                if (genome.anonymous[2 * i] == genome.anonymous[2 * i + 1] && genome.anonymous[2 * i] != 0) {
                    return true;
                }
            }
            return false;
        }

        void GeneInterpreter.Interpret(Genome genome, Entity entity) {
            int repeats = 0;
            for (int i = 0; i < NUM_DIVERSITY_GENES; ++i) {
                if (genome.anonymous[2 * i] == genome.anonymous[2 * i + 1]) {
                    repeats += 1;
                }
            }
            float coi = repeats / (float)NUM_DIVERSITY_GENES;
            entity.WatchedAttributes.GetOrAddTreeAttribute(EntityBehaviorGenetics.Code).SetFloat("coi", coi);
        }
    }
}
