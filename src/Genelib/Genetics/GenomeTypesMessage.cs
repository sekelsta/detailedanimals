using ProtoBuf;
using System.Collections.Generic;

using Vintagestory.API.Common;

namespace Genelib {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class GenomeTypesMessage {
        public string[] AssetLocations;
        public GenomeType[] GenomeTypes;

        public GenomeTypesMessage() {}

        public GenomeTypesMessage(Dictionary<AssetLocation, GenomeType> types) {
            AssetLocations = new string[types.Count];
            GenomeTypes = new GenomeType[types.Count];

            int i = 0;
            foreach (var item in types) {
                AssetLocations[i] = item.Key;
                GenomeTypes[i] = item.Value;
                i++;
            }
        }
    }
}
