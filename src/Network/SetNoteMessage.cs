using ProtoBuf;

namespace DetailedAnimals.Network {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SetNoteMessage {
        public long entityId;
        public string note;
    }
}
