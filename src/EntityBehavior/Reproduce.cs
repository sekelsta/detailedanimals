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

        public Reproduce(Entity entity)
          : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes) {
            GenomeType = GenomeType.FromLocation(
                AssetLocation.Create(attributes["genomeType"].AsString(), entity.Code.Domain)
            );
            initializers = attributes["initializers"].AsArray<string>();
            entity.World.Logger.Notification("Reproduce initialized"); //TODO: This should happen on patched game domain entities too
        }

        public override void AfterInitialized(bool onFirstSpawn) {
            if (onFirstSpawn) {
                BlockPos blockPos = entity.ServerPos.AsBlockPos;
                ClimateCondition climate = entity.Api.World.BlockAccessor.GetClimateAt(blockPos);
                Random random = entity.World.Rand;
                bool heterogametic = random.Next(2) == 0;
                AlleleFrequencies frequencies = GenomeType.ChooseInitializer(initializers, climate, blockPos.Y, random);
                Genome = new Genome(frequencies, heterogametic, random);
                entity.World.Logger.Notification(Genome.ToString());
            }
        }

        public override void GetInfoText(StringBuilder infotext) {
            base.GetInfoText(infotext);
            infotext.AppendLine("EntityBehavior Reproduce");
        }

        public override string PropertyName() => "reproduce";
    }
}
