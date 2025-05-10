using ProtoBuf;
using System.Collections.Generic;

namespace Genelib {
    [ProtoContract]
    public struct NameMapping {
        private Dictionary<string, int> geneMap;
        [ProtoMember(1)]
        private string[] geneArray;
        [ProtoMember(2)]
        private string[][] alleleArrays;
        private Dictionary<string, byte>[] alleleMaps;

        public int GeneCount { get => geneArray.Length; }
        public int AlleleCount(int gene) {
            return alleleArrays[gene].Length;
        }

        public bool TryGetGeneID(string name, out int geneId) {
            return geneMap.TryGetValue(name, out geneId);
        }

        public int GeneID(string name) {
            return geneMap[name];
        }

        public string GeneName(int id) {
            return geneArray[id];
        }

        public string AlleleName(int geneID, int alleleID) {
            return alleleArrays[geneID][alleleID];
        }

        public byte AlleleID(int geneID, string alleleName) {
            return alleleMaps[geneID][alleleName];
        }

        public NameMapping() {
            geneArray = new string[0];
        }

        public NameMapping(string[] geneArray, string[][] alleleArrays) {
            this.geneArray = geneArray;
            this.alleleArrays = alleleArrays;
            InitializeMaps();
        }

        [ProtoAfterDeserialization]
        private void InitializeMaps() {
            geneMap = new Dictionary<string, int>();
            alleleMaps = new Dictionary<string, byte>[geneArray.Length];
            for (int gene = 0; gene < geneArray.Length; ++gene) {
                geneMap[GeneName(gene)] = gene;
                alleleMaps[gene] = new Dictionary<string, byte>();
                for (byte allele = 0; allele < alleleArrays[gene].Length; ++allele) {
                    alleleMaps[gene][alleleArrays[gene][allele]] = allele;
                }
            }
        }
    }
}
