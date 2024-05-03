using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Genelib {
    public class GenomeType {
        private static volatile Dictionary<AssetLocation, GenomeType> loaded = new Dictionary<AssetLocation, GenomeType>();
        private static Dictionary<string, GeneInterpreter> interpreterMap = new Dictionary<string, GeneInterpreter>();

        public static void RegisterInterpreter(string name, GeneInterpreter interpreter) {
            interpreterMap[name] = interpreter;
        }

        public NameMapping Autosomal { get; protected set; }
        public NameMapping XZ { get; protected set; }
        public NameMapping YW { get; protected set; }
        private Dictionary<string, GeneInitializer> initializers = new Dictionary<string, GeneInitializer>();
        public GeneInterpreter[] Interpreters { get; protected set; }

        public SexDetermination SexDetermination { get; protected set; } = SexDetermination.XY;
        private AlleleFrequencies defaultFrequencies;
        public AlleleFrequencies DefaultFrequencies {
            get {
                if (defaultFrequencies == null) {
                    defaultFrequencies = new AlleleFrequencies(this);
                }
                return defaultFrequencies;
                }
            private set {
                defaultFrequencies = value;
            }
        }
        public string Name { get; private set; }

        private GenomeType(string name, JsonObject attributes) {
            this.Name = name;
            JsonObject genes = attributes["genes"];
            Autosomal = parse(genes, "autosomal");
            XZ = parse(genes, "xz");
            YW = parse(genes, "yw");

            if (attributes.KeyExists("initializers")) {
                JsonObject initialization = attributes["initializers"];
                foreach (JProperty jp in ((JObject) initialization.Token).Properties()) {
                    initializers[jp.Name] = new GeneInitializer(this, new JsonObject(jp.Value));
                }
            }
            if (attributes.KeyExists("sexdetermination")) {
                SexDetermination = SexDeterminationExtensions.Parse(attributes["sexdetermination"].AsString());
            }
            string[] interpreterNames = attributes["interpreters"].AsArray<string>();
            Interpreters = new GeneInterpreter[interpreterNames.Length];
            for (int i = 0; i < interpreterNames.Length; ++i) {
                Interpreters[i] = interpreterMap[interpreterNames[i]];
            }
        }

        private NameMapping parse(JsonObject json, string key) {
            NameMapping mapping = new NameMapping();
            if (!json.KeyExists(key)) {
                mapping.geneArray = new string[0];
                return mapping;
            }
            JsonObject[] genes = json[key].AsArray();
            mapping.geneMap = new Dictionary<string, int>();
            mapping.geneArray = new string[genes.Length];
            mapping.alleleArrays = new string[genes.Length][];
            mapping.alleleMaps = new Dictionary<string, byte>[genes.Length];
            for (int gene = 0; gene < genes.Length; ++gene) {
                JProperty jp = ((JObject) genes[gene].Token).Properties().First();
                string geneName = jp.Name;
                mapping.geneMap[geneName] = gene;
                mapping.geneArray[gene] = geneName;
                mapping.alleleArrays[gene] = new JsonObject(jp.Value).AsArray<string>();
                mapping.alleleMaps[gene] = new Dictionary<string, byte>();
                for (byte allele = 0; allele < mapping.alleleArrays[gene].Length; ++allele) {
                    mapping.alleleMaps[gene][mapping.alleleArrays[gene][allele]] = allele;
                }
            }
            return mapping;
        }

        public static GenomeType Load(IAsset asset) {
            AssetLocation key = asset.Location.CopyWithPathPrefixAndAppendixOnce("genetics/", ".json");
            if (!loaded.ContainsKey(key)) {
                loaded[key] = new GenomeType(key.CopyWithPath(key.PathOmittingPrefixAndSuffix("genetics/", ".json")).ToString(), JsonObject.FromJson(asset.ToText()));
            }
            return loaded[key];
        }

        public static GenomeType Get(AssetLocation location) {
            AssetLocation key = location.CopyWithPathPrefixAndAppendixOnce("genetics/", ".json");
            return loaded[key];
        }

        public GeneInitializer Initializer(string name) {
            return initializers[name];
        }

        public AlleleFrequencies ChooseInitializer(string[] initializerNames, ClimateCondition climate, int y, Random random) {
            List<GeneInitializer> valid = new List<GeneInitializer>();
            // If no list provided, default to all being valid
            if (initializerNames == null) {
                foreach (KeyValuePair<string, GeneInitializer> entry in initializers) {
                    addIfValid(valid, climate, y, entry.Value);
                }
            }
            else {
                for (int i = 0; i < initializerNames.Length; ++i) {
                    addIfValid(valid, climate, y, initializers[initializerNames[i]]);
                }
            }
            if (valid.Count == 0) {
                return null;
            }
            return valid[random.Next(valid.Count)].Frequencies;
        }

        private void addIfValid(List<GeneInitializer> valid, ClimateCondition climate, int y, GeneInitializer ini) {
            if (ini.CanSpawnAt(climate, y)) {
                valid.Add(ini);
            }
        }
    }
}
