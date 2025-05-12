using ProtoBuf;

namespace DetailedAnimals.Network {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ToggleBreedingMessage {
        public long entityId;
        public bool preventBreeding;
    }
}
