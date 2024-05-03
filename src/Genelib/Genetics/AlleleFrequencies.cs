using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

using Vintagestory.API.Datastructures;

using Genelib.Extensions;

namespace Genelib {
    public class AlleleFrequencies {
        public GenomeType ForType { get; private set; }
        public float[][] Autosomal { get; private set; }
        public float[][] XZ { get; private set; }
        public float[][] YW { get; private set; }

        public AlleleFrequencies(GenomeType type) {
            ForType = type;
            Autosomal = new float[type.Autosomal.GeneCount][];
            XZ = new float[type.XZ.GeneCount][];
            YW = new float[type.YW.GeneCount][];
            // Ok to leave array contents null
        }

        public AlleleFrequencies(GenomeType type, JsonObject json) : this(type) {
            parseFrequencies(json, "autosomal", Autosomal, type.Autosomal);
            if (!parseFrequencies(json, "xz", XZ, type.XZ)) {
                parseFrequencies(json, "sexlinked", XZ, type.XZ);
            }
            parseFrequencies(json, "yw", YW, type.YW);
        }

        private bool parseFrequencies(JsonObject json, string key, float[][] frequencies, NameMapping mappings) {
            if (!json.KeyExists(key)) {
                return false;
            }
            JsonObject genesObject = json[key];
            foreach (JProperty jp in ((JObject) genesObject.Token).Properties()) {
                string geneName = jp.Name;
                int geneID = mappings.GeneID(geneName);
                string defaultAlleleName = null;
                JObject jsonFrequencies = (JObject) jp.Value;
                List<float> list = new List<float>();
                if (jsonFrequencies.ContainsKey("default")) {
                    object o = ((JValue) jsonFrequencies.GetValue("default")).Value;
                    if (o is string) {
                        defaultAlleleName = (string) o;
                    }
                }
                int defaultAlleleID = defaultAlleleName == null ? 0 : mappings.AlleleID(geneID, defaultAlleleName);
                foreach (JProperty jf in jsonFrequencies.Properties()) {
                    string alleleName = jf.Name;
                    if (alleleName == "default" && defaultAlleleName != null) {
                        continue;
                    }
                    int alleleID = mappings.AlleleID(geneID, alleleName);
                    list.EnsureSize(alleleID + 1);
                    list[alleleID] = new JsonObject(jf.Value).AsFloat();
                }
                float sum = 0;
                foreach (float f in list) {
                    sum += f;
                }
                list.EnsureSize(defaultAlleleID + 1);
                list[defaultAlleleID] = Math.Max(0, 1 - sum);
                float s = 1;
                if (sum > 1) {
                    s = 1 / sum;
                }
                frequencies[geneID] = new float[list.Count];
                frequencies[geneID][0] = s * list[0];
                for (int i = 1; i < frequencies[geneID].Length; ++i) {
                    frequencies[geneID][i] = frequencies[geneID][i - 1] + s * list[i];
                }
            }
            return true;
        }
    }
}
