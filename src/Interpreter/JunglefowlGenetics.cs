using Genelib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace DetailedAnimals {
    public struct ChickenPhenotype {
        public int Shanks;
        public int Body;
    }

    public class JunglefowlGenetics : GeneInterpreter {
        private static ChickenPhenotype getPhenotype(Genome genome) {
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

            const int SHANK_DARKWHITE = 0;
            const int SHANK_DARKYELLOW = 1;
            const int SHANK_BLACK = 2;
            const int SHANK_BLACKYELLOW = 3;
            const int SHANK_WHITE = 4;
            const int SHANK_YELLOW = 5;

            ChickenPhenotype phenotype;
            bool shanksyellow = genome.Homozygous("whiteshanks", "yellow");

            if (genome.HasAutosomal("extension", "black")) {
                phenotype.Shanks = shanksyellow ? SHANK_BLACKYELLOW : SHANK_BLACK;
                if (genome.Homozygous("bluesplash", "bluesplash")) {
                    phenotype.Body = splash;
                }
                else if (genome.HasAutosomal("bluesplash", "bluesplash")) {
                    phenotype.Body = blue;
                }
                else {
                    phenotype.Body = black;
                }
            }
            else if (genome.HasAutosomal("extension", "birchen")) {
                phenotype.Shanks = shanksyellow ? SHANK_BLACKYELLOW : SHANK_BLACK;
                if (genome.Homozygous("bluesplash", "bluesplash")) {
                    phenotype.Body = birchensplash;
                }
                else if (genome.HasAutosomal("bluesplash", "bluesplash")) {
                    phenotype.Body = birchenblue;
                }
                else {
                    phenotype.Body = birchen;
                }
            }
            else {
                phenotype.Shanks = shanksyellow ? SHANK_DARKYELLOW : SHANK_DARKWHITE;

                if (genome.Homozygous("bluesplash", "bluesplash")) {
                    phenotype.Body = duckwingsplash;
                }
                else if (genome.HasAutosomal("bluesplash", "bluesplash")) {
                    phenotype.Body = duckwingblue;
                }
                else {
                    phenotype.Body = duckwing;
                }
            }

            if (genome.HasXZ("dermalmelanin", "inhibited")) {
                phenotype.Shanks = shanksyellow ? SHANK_YELLOW : SHANK_WHITE;
            }

            if (genome.Homozygous("tyrosinase", "white")) {
                phenotype.Body = white;
            }
            return phenotype;
        }

        public string Name => "Junglefowl";

        void GeneInterpreter.MatchPhenotype(EntityBehaviorGenetics genetics) {
            Entity entity = genetics.entity;
            Genome genome = genetics.Genome;
            int texture = entity.WatchedAttributes.GetInt("textureIndex", 0);
            if (entity.Code == "game:chicken-rooster") {
                int blacktail = 1;
                int black = 2;
                int blue = 3;
                if (texture == black || texture == blacktail || texture == blue) {
                    genome.SetHomozygous("extension", "black");
                }
                if (texture == blue) {
                    genome.SetAutosomal("bluesplash", 0, "bluesplash");
                }
                else if (texture == blacktail) {
                    // Treat as splash for now
                    genome.SetHomozygous("bluesplash", "bluesplash");
                }
            }
            else if (entity.Code == "game:chicken-hen") {
                int black = 0;
                int blue = 1;
                int white = 2;
                int splash = 3;
                int birchensplash = 7;
                if (texture == white) {
                    genome.SetHomozygous("tyrosinase", "white");
                }
                else if (texture == black || texture == blue || texture == splash) {
                    genome.SetAutosomal("extension", 0, "black");
                }
                else if (texture == birchensplash) {
                    genome.SetAutosomal("extension", 0, "birchen");
                }
                if (texture == blue) {
                    genome.SetAutosomal("bluesplash", 0, "bluesplash");
                }
                else if (texture == splash || texture == birchensplash) {
                    genome.SetHomozygous("bluesplash", "bluesplash");
                }
            }
            else if (entity.Code == "game:chicken-baby") {
                int blue = 2;
                int black = 4;
                if (texture == black || texture == blue) {
                    genome.SetAutosomal("extension", 0, "black");
                }
                if (texture == blue) {
                    genome.SetAutosomal("bluesplash", 0, "bluesplash");
                }
            }
        }

        void GeneInterpreter.Interpret(EntityBehaviorGenetics genetics) {

        }

        ITexPositionSource GeneInterpreter.GetTextureSource(EntityBehaviorGenetics genetics, ref EnumHandling handling) {
            Entity entity = genetics.entity;
            Genome genome = genetics.Genome;

            ChickenPhenotype phenotype = getPhenotype(genome);
            if (entity.Code.Path.EndsWith("-baby") || entity.Code.Path.EndsWith("-chick")) {
                handling = EnumHandling.PreventSubsequent;
                return (entity.Api as ICoreClientAPI).Tesselator.GetTextureSource(entity, null, phenotype.Body);
            }

            MiniDictionary mapping = new MiniDictionary(2);
            mapping["chicken"] = phenotype.Body == 0 
                ? entity.Properties.Client.Textures["chicken"].Baked.TextureSubId
                : entity.Properties.Client.Textures["chicken"].Baked.BakedVariants[phenotype.Body].TextureSubId;
            mapping["shanks"] = phenotype.Shanks == 0 
                ? entity.Properties.Client.Textures["shanks"].Baked.TextureSubId
                : entity.Properties.Client.Textures["shanks"].Baked.BakedVariants[phenotype.Shanks].TextureSubId;

            Size2i atlasSize = DetailedAnimalsModSystem.ClientAPI.EntityTextureAtlas.Size;

            handling = EnumHandling.PreventSubsequent;
            return new DictionaryTextureSource() { Mapping = mapping, Atlas = DetailedAnimalsModSystem.ClientAPI.EntityTextureAtlas };
        }
    }
}
