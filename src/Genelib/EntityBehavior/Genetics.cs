using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Genelib {
    public class Genetics : EntityBehavior {
        public GenomeType GenomeType { get; protected set; }
        private Genome genome;
        public Genome Genome {
            get => genome;
            protected set {
                TreeAttribute geneticsTree = (TreeAttribute) entity.WatchedAttributes.GetOrAddTreeAttribute(PropertyName());
                if (value.autosomal == null) {
                    geneticsTree.RemoveAttribute("autosomal");
                }
                else {
                    geneticsTree.SetAttribute("autosomal", new ByteArrayAttribute(value.autosomal));
                }
                if (value.anonymous == null) {
                    geneticsTree.RemoveAttribute("anonymous");
                }
                else {
                    geneticsTree.SetAttribute("anonymous", new ByteArrayAttribute(value.anonymous));
                }
                if (value.primary_xz == null) {
                    geneticsTree.RemoveAttribute("primary_xz");
                }
                else {
                    geneticsTree.SetAttribute("primary_xz", new ByteArrayAttribute(value.primary_xz));
                }
                if (value.secondary_xz == null) {
                    geneticsTree.RemoveAttribute("secondary_xz");
                }
                else {
                    geneticsTree.SetAttribute("secondary_xz", new ByteArrayAttribute(value.secondary_xz));
                }
                if (value.yw == null) {
                    geneticsTree.RemoveAttribute("yw");
                }
                else {
                    geneticsTree.SetAttribute("yw", new ByteArrayAttribute(value.yw));
                }
                genome = value;
                entity.WatchedAttributes.MarkPathDirty(PropertyName());
            }
        }
        protected string[] initializers;
        protected bool isMale = false;
        protected AlleleFrequencies defaultFrequencies;

        public Genetics(Entity entity)
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
            initializers = attributes["initializers"].AsArray<string>();
            if (attributes.KeyExists("male")) {
                isMale = attributes["male"].AsBool();
            }

            TreeAttribute geneticsTree = (TreeAttribute) entity.WatchedAttributes.GetTreeAttribute(PropertyName());
            if (geneticsTree != null) {
                byte[] autosomal = (geneticsTree.GetAttribute("autosomal") as ByteArrayAttribute)?.value;
                byte[] anonymous = (geneticsTree.GetAttribute("anonymous") as ByteArrayAttribute)?.value;
                byte[] primary_xz = (geneticsTree.GetAttribute("primary_xz") as ByteArrayAttribute)?.value;
                byte[] secondary_xz = (geneticsTree.GetAttribute("secondary_xz") as ByteArrayAttribute)?.value;
                byte[] yw = (geneticsTree.GetAttribute("yw") as ByteArrayAttribute)?.value;
                Genome = new Genome(GenomeType, autosomal, anonymous, primary_xz, secondary_xz, yw);
                GeneticsModSystem.API.Logger.Notification("Loading genome from save");
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
                GeneticsModSystem.API.Logger.Notification("Randomizing genome for new spawn");
            }
            else if (Genome == null) {
                Genome = new Genome(defaultFrequencies, heterogametic, random);
                GeneticsModSystem.API.Logger.Notification("Creating default genome");
            }
        }

        public override void GetInfoText(StringBuilder infotext) {
            base.GetInfoText(infotext);
            infotext.AppendLine("EntityBehavior Genetics");
        }

        public override string PropertyName() => "genetics";
    }
}
