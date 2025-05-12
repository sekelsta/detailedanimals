using ProtoBuf;

namespace DetailedAnimals.Network {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SetNameMessage {
        public long entityId;
        public string name;
    }
}
