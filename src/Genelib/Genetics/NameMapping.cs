using System.Collections.Generic;

namespace Genelib {
    public struct NameMapping {
        internal Dictionary<string, int> geneMap;
        internal string[] geneArray;
        internal string[][] alleleArrays;
        internal Dictionary<string, byte>[] alleleMaps;

        public int GeneCount { get => geneArray.Length; }
        public int AlleleCount(int gene) {
            return alleleArrays[gene].Length;
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
    }
}
