
using Vintagestory.API.Common.Entities;
using Genelib;

namespace VintageInheritance {
    static class PigGenetics {
        public static void Finalize(Genome genome, AlleleFrequencies frequencies, Entity entity) {
            genome.SetNotHomozygous("KIT", "lethalwhite", frequencies, "wildtype");
        }

        public static void Interpret(Genome genome, Entity entity) {
            entity.WatchedAttributes.SetInt("textureIndex", getTextureIndex(genome));
        }

        private static int getTextureIndex(Genome genome) {
            int agouti = 0;
            int black = 1;
            int red = 2;
            int agoutiblack = 3;
            int redblack = 4;
            int white = 5;
            int whiteblack = 6;
            if (genome.HasAutosomal("KIT", "lesswhite", "white", "morewhite", "lethalwhite")) {
                return white;
            }
            if (genome.HasAutosomal("extension", "black")) {
                return black;
            }
            bool spot = genome.HasAutosomal("extension", "spot");
            bool patch = genome.HasAutosomal("KIT", "patch");
            if (spot) {
                if (patch) {
                    return whiteblack;
                }
                else if (genome.HasAutosomal("extension", "red")) {
                    return redblack;
                }
                return agoutiblack;
            }
            if (genome.Homozygous("extension", "red")) {
                return red;
            }
            return agouti;
        }
    }
}
