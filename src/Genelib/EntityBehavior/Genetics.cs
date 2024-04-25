using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Genelib {
    public class Genetics : EntityBehavior {
        private static Dictionary<string, Action<Genome, Entity>> interpreters = new Dictionary<string, Action<Genome, Entity>>();
        private static Dictionary<string, Action<Genome, Entity>> finalizers = new Dictionary<string, Action<Genome, Entity>>();

        public GenomeType GenomeType { get; protected set; }
        private Genome genome;
        public Genome Genome {
            get => genome;
            protected set {
                genome = value;
                GenomeModified();
            }
        }
        protected string[] initializers;
        protected string[] interpreterNames;
        protected string[] finalizerNames;
        protected bool isMale = false;
        protected AlleleFrequencies defaultFrequencies;

        public Genetics(Entity entity)
          : base(entity)
        {
        }

        // Called when a genome has just been set. Expected to set entity attributes based on genome contents.
        // The action passed in here should never modify the genome.
        public static void RegisterInterpreter(string name, Action<Genome, Entity> interpreter) {
            interpreters[name] = interpreter;
        }

        // Called when a new genome is being created for an entity with no parents.
        // This action is expected to modify the genome in ways not easily handled by gene initializers,
        // such as to ensure lethal genes are not homozygous.
        public static void RegisterFinalizer(string name, Action<Genome, Entity> finalizer) {
            finalizers[name] = finalizer;
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
            interpreterNames = attributes["interpreters"].AsArray<string>() ?? arrayOrNull(attributes["interpreter"].AsString());
            finalizerNames = attributes["finalizers"].AsArray<string>() ?? arrayOrNull(attributes["finalizer"].AsString());
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

        private string[] arrayOrNull(string s) {
            if (s == null) {
                return null;
            }
            return new string[] { s };
        }

        public override void AfterInitialized(bool onFirstSpawn) {
            if (entity.World.Side != EnumAppSide.Server) {
                return;
            }
            Random random = entity.World.Rand;
            bool heterogametic = GenomeType.SexDetermination.Heterogametic(isMale);
            if (onFirstSpawn || Genome == null) {
                if (onFirstSpawn) {
                    BlockPos blockPos = entity.ServerPos.AsBlockPos;
                    ClimateCondition climate = entity.Api.World.BlockAccessor.GetClimateAt(blockPos);
                    AlleleFrequencies frequencies = GenomeType.ChooseInitializer(initializers, climate, blockPos.Y, random)
                        ?? defaultFrequencies;
                    Genome = new Genome(frequencies, heterogametic, random);
                }
                else {
                    Genome = new Genome(defaultFrequencies, heterogametic, random);
                }
                if (finalizerNames != null) {
                    foreach (string name in finalizerNames) {
                        finalizers[name]?.Invoke(genome, entity);
                    }
                }
            }
        }

        public void GenomeModified() {
            TreeAttribute geneticsTree = (TreeAttribute) entity.WatchedAttributes.GetOrAddTreeAttribute(PropertyName());
            if (genome.autosomal == null) {
                geneticsTree.RemoveAttribute("autosomal");
            }
            else {
                geneticsTree.SetAttribute("autosomal", new ByteArrayAttribute(genome.autosomal));
            }
            if (genome.anonymous == null) {
                geneticsTree.RemoveAttribute("anonymous");
            }
            else {
                geneticsTree.SetAttribute("anonymous", new ByteArrayAttribute(genome.anonymous));
            }
            if (genome.primary_xz == null) {
                geneticsTree.RemoveAttribute("primary_xz");
            }
            else {
                geneticsTree.SetAttribute("primary_xz", new ByteArrayAttribute(genome.primary_xz));
            }
            if (genome.secondary_xz == null) {
                geneticsTree.RemoveAttribute("secondary_xz");
            }
            else {
                geneticsTree.SetAttribute("secondary_xz", new ByteArrayAttribute(genome.secondary_xz));
            }
            if (genome.yw == null) {
                geneticsTree.RemoveAttribute("yw");
            }
            else {
                geneticsTree.SetAttribute("yw", new ByteArrayAttribute(genome.yw));
            }
            entity.WatchedAttributes.MarkPathDirty(PropertyName());
            if (interpreterNames != null) {
                foreach (string name in interpreterNames) {
                    interpreters[name]?.Invoke(genome, entity);
                }
            }
        }

        public override string PropertyName() => "genetics";
    }
}
