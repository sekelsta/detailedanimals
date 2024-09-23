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
            int white = 1;
            int black = 2;
            int blue = 3;
            int splash = 4;
            int birchen = 5;
            int birchenblue = 6;
            int birchensplash = 7;
            int duckwingblue = 8;
            int duckwingsplash = 9;
            if (genome.Homozygous("tyrosinase", "white")) {
                return white;
            }
            if (genome.HasAutosomal("extension", "black")) {
                if (genome.Homozygous("bluesplash", "bluesplash")) {
                    return splash;
                }
                else if (genome.HasAutosomal("bluesplash", "bluesplash")) {
                    return blue;
                }
                return black;
            }
            else if (genome.HasAutosomal("extension", "birchen")) {
                if (genome.Homozygous("bluesplash", "bluesplash")) {
                    return birchensplash;
                }
                else if (genome.HasAutosomal("bluesplash", "bluesplash")) {
                    return birchenblue;
                }
                return birchen;
            }
            if (genome.Homozygous("bluesplash", "bluesplash")) {
                return duckwingsplash;
            }
            else if (genome.HasAutosomal("bluesplash", "bluesplash")) {
                return duckwingblue;
            }
            return duckwing;
        }
    }
}
