using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Genelib {
    public class Reproduce : EntityBehavior {
        public GenomeType GenomeType { get; protected set; }
        public Genome Genome { get; protected set; }
        protected string[] initializers;
        protected bool isMale = false;
        protected AlleleFrequencies defaultFrequencies;

        public Reproduce(Entity entity)
          : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes) {
            GenomeType = GenomeType.FromLocation(
                AssetLocation.Create(attributes["genomeType"].AsString(), entity.Code.Domain)
            );
            initializers = attributes["initializers"].AsArray<string>();
            if (attributes.KeyExists("male")) {
                isMale = attributes["male"].AsBool();
            }
            if (attributes.KeyExists("defaultinitializer")) {
                defaultFrequencies = GenomeType.Initializer(attributes["default"].AsString()).Frequencies;
            }
            else {
                defaultFrequencies = GenomeType.DefaultFrequencies;
            }
        }

        public override void AfterInitialized(bool onFirstSpawn) {
            if (entity.World.Side != EnumAppSide.Server) {
                return;
            }
            Random random = entity.World.Rand;
            bool heterogametic = GenomeType.SexDetermination.Heterogametic(isMale);
            if (onFirstSpawn) {
                BlockPos blockPos = entity.ServerPos.AsBlockPos;
                ClimateCondition climate = entity.Api.World.BlockAccessor.GetClimateAt(blockPos);
                AlleleFrequencies frequencies = GenomeType.ChooseInitializer(initializers, climate, blockPos.Y, random)
                    ?? defaultFrequencies;
                Genome = new Genome(frequencies, heterogametic, random);
            }
            else if (Genome == null) {
                Genome = new Genome(defaultFrequencies, heterogametic, random);
            }
        }

        public override void GetInfoText(StringBuilder infotext) {
            base.GetInfoText(infotext);
            infotext.AppendLine("EntityBehavior Reproduce");
        }

        public override string PropertyName() => "reproduce";
    }
}
