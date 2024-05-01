using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Genelib {
    public class Genetics : EntityBehavior {
        public const string Code = "genetics";
        private static Dictionary<string, Action<Genome, Entity>> interpreters = new Dictionary<string, Action<Genome, Entity>>();
        private static Dictionary<string, Action<Genome, AlleleFrequencies, Entity>> finalizers = new Dictionary<string, Action<Genome, AlleleFrequencies, Entity>>();

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
        public static void RegisterFinalizer(string name, Action<Genome, AlleleFrequencies, Entity> finalizer) {
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
            if (entity.World.Side != EnumAppSide.Server) {
                return;
            }
            if (onFirstSpawn || Genome == null) {
                Random random = entity.World.Rand;
                bool heterogametic = GenomeType.SexDetermination.Heterogametic(isMale);
                AlleleFrequencies frequencies = null;
                if (onFirstSpawn) {
                    BlockPos blockPos = entity.ServerPos.AsBlockPos;
                    ClimateCondition climate = entity.Api.World.BlockAccessor.GetClimateAt(blockPos);
                    frequencies = GenomeType.ChooseInitializer(initializers, climate, blockPos.Y, random)
                        ?? defaultFrequencies;
                }
                else {
                    frequencies = defaultFrequencies;
                }
                Genome = new Genome(frequencies, heterogametic, random);
                Genome.Mutate(GeneticsModSystem.MutationRate, random);
                if (finalizerNames != null) {
                    foreach (string name in finalizerNames) {
                        finalizers[name]?.Invoke(genome, frequencies, entity);
                    }
                }
            }
        }

        public void GenomeModified() {
            TreeAttribute geneticsTree = (TreeAttribute) entity.WatchedAttributes.GetOrAddTreeAttribute(PropertyName());
            genome.AddToTree(geneticsTree);
            entity.WatchedAttributes.MarkPathDirty(PropertyName());
            if (interpreterNames != null) {
                foreach (string name in interpreterNames) {
                    interpreters[name]?.Invoke(genome, entity);
                }
            }
        }

        public override string PropertyName() => Code;
    }
}
