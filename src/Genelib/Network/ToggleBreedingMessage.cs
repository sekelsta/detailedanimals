using ProtoBuf;

namespace Genelib.Network {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ToggleBreedingMessage {
        public long entityId;
        public bool preventBreeding;
    }
}
