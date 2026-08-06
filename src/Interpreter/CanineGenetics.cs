using Genelib;
using Genelib.Extensions;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace DetailedAnimals {
    public class CanineGenetics : GeneInterpreter {
        public string Name => "Canine";

        void GeneInterpreter.Interpret(EntityBehaviorGenetics genetics) {
            Entity entity = genetics.entity;
            Genome genome = genetics.Genome;
            if (entity.Api.Side != EnumAppSide.Server) {
                return;
            }
            int prevTexture = entity.WatchedAttributes.GetInt("textureIndex", 0);
            int newTexture = getTextureIndex(genome, entity, prevTexture);
            if (newTexture != prevTexture) {
                entity.WatchedAttributes.SetInt("textureIndex", newTexture);
            }
        }

        private static int getTextureIndex(Genome genome, Entity entity, int prevTexture) {
            if (genome.HasAllele("K", "B")) {
                return 0;
            }

            if (entity.Code.Path.EndsWith("-pup")) {
                return 1;
            }

            // Try to keep same texture as before, if possible
            if (prevTexture > 0) return prevTexture;

            // Want a random value between 1 and 9, inclusive, that stays constant per entity
            long uuid = entity.UniqueID();
            int texture = (int)(uuid * 17 * 23 * 191 % 9) + 1;

            return texture;
        }

        void GeneInterpreter.MatchPhenotype(EntityBehaviorGenetics genetics) {
            Entity entity = genetics.entity;
            Genome genome = genetics.Genome;

            // Normally would default to 0, but here I care whether 0 is explicitly present
            int texture = entity.WatchedAttributes.GetInt("textureIndex", -1);

            if (texture == 0) {
                genome.SetAutosomal("K", 0, "B");
            }
        }
    }
}
