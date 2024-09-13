using Genelib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace TruthBeauty {
    public class JunglefowlGenetics : GeneInterpreter {
        void GeneInterpreter.Interpret(Genome genome, Entity entity) {
            entity.WatchedAttributes.SetInt("textureIndex", getTextureIndex(genome));
        }

        private static int getTextureIndex(Genome genome) {
            int duckwing = 0;
            int black = 1;
            int white = 2;
            int blue = 3;
            int bluesplash = 4;
            if (genome.Homozygous("tyrosinase", "white")) {
                return white;
            }
            if (genome.HasAutosomal("extension", "black")) {
                if (genome.Homozygous("bluesplash", "bluesplash")) {
                    return bluesplash;
                }
                else if (genome.HasAutosomal("bluesplash", "bluesplash")) {
                    return blue;
                }
                return black;
            }
            return duckwing;
        }
    }
}
