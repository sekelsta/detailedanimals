using Genelib.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Genelib {
    public class EntityBehaviorGenetics : EntityBehavior {
        public const string Code = "genelib.genetics";

        protected GenomeType GenomeType { get; set; }
        private Genome genome;
        public Genome Genome {
            get => genome;
            set {
                genome = value;
                GenomeModified();
            }
        }
        protected string[] initializers;
        protected AlleleFrequencies defaultFrequencies;

        public EntityBehaviorGenetics(Entity entity)
          : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes) {
            GenomeType = GenomeType.Get(
                AssetLocation.Create(attributes["genomeType"].AsString(), entity.Code.Domain)
            );
            if (attributes.KeyExists("defaultinitializer")) {
                defaultFrequencies = GenomeType.Initializer(attributes["default"].AsString()).Frequencies;
            }
            else {
                defaultFrequencies = GenomeType.DefaultFrequencies;
            }
            initializers = attributes["initializers"].AsArray<string>() ?? arrayOrNull(attributes["initializer"].AsString());

            TreeAttribute geneticsTree = (TreeAttribute) entity.WatchedAttributes.GetTreeAttribute("genetics");
            if (geneticsTree != null) {
                Genome = new Genome(GenomeType, geneticsTree);
            }
        }

        private string[] arrayOrNull(string s) {
            if (s == null) {
                return null;
            }
            return new string[] { s };
        }

        public override void AfterInitialized(bool onFirstSpawn) {
            if (entity.World.Side == EnumAppSide.Server) {
                if (Genome == null) {
                    Random random = entity.World.Rand;
                    bool heterogametic = GenomeType.SexDetermination.Heterogametic(entity.IsMale());
                    AlleleFrequencies frequencies = null;
                    if (onFirstSpawn) {
                        BlockPos blockPos = entity.Pos.AsBlockPos;
                        ClimateCondition climate = entity.Api.World.BlockAccessor.GetClimateAt(blockPos);
                        frequencies = GenomeType.ChooseInitializer(initializers, climate, blockPos.Y, random)
                            ?? defaultFrequencies;
                    }
                    else {
                        frequencies = defaultFrequencies;
                    }
                    Genome = new Genome(frequencies, heterogametic, random);
                    Genome.Mutate(AnimalConfig.MutationRate, random);
                    foreach (GeneInterpreter interpreter in Genome.Type.Interpreters) {
                        interpreter.Finalize(Genome, frequencies, random);
                    }
                }
            }
            foreach (GeneInterpreter interpreter in Genome.Type.Interpreters) {
                interpreter.Interpret(this);
            }
        }

        public void GenomeModified() {
            if (entity.World.Side == EnumAppSide.Client) {
                return;
            }
            TreeAttribute geneticsTree = (TreeAttribute) entity.WatchedAttributes.GetOrAddTreeAttribute("genetics");
            genome.AddToTree(geneticsTree);
            entity.WatchedAttributes.MarkPathDirty("genetics");
        }

        public override ITexPositionSource GetTextureSource(ref EnumHandling handling) {
            ITexPositionSource source = null;
            foreach (GeneInterpreter interpreter in Genome.Type.Interpreters) {
                source = interpreter.GetTextureSource(this, ref handling);
                if (handling == EnumHandling.PreventSubsequent) {
                    return source;
                }
            }
            return source;
        }

        public override string PropertyName() => Code;
    }
}
