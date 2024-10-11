using ProtoBuf;

namespace Genelib.Network {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SetNameMessage {
        public long entityId;
        public string name;
    }
}
