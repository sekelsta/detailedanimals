using Genelib;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace DetailedAnimals {
    public class PigSizeGenetics : GeneInterpreter {
        public string Name => "PigSize";

        void GeneInterpreter.Interpret(EntityBehaviorGenetics genetics) {
            Entity entity = genetics.entity;
            if (entity.World.Side == EnumAppSide.Client) {
                return;
            }
            Genome genome = genetics.Genome;

            int sizeGeneCount = genome.BitwiseSum("size");
            var sizeRange = genome.Type.Bitwise.TryGetRange("size");
            int sizeTotalAlleles = sizeRange.End.Value - sizeRange.Start.Value;
            float sizeBase = (float)sizeGeneCount / sizeTotalAlleles;
            float sizeMajor = genome.HasAllele("size_major", "large") ? genome.IsHomozygous("size_major", "large") ? 1 : 0.75f : 0;
            entity.WatchedAttributes.SetFloat("geneticSize", (0.5f + sizeBase) * (1 + 0.5f * sizeMajor));
        }
    }
}
