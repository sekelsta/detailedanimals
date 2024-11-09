using ProtoBuf;

namespace Genelib.Network {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SetNoteMessage {
        public long entityId;
        public string note;
    }
}
