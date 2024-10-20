// Based on PetAI's BehaviorConsiderHumanFoodForPetsToo
using Vintagestory.API.Common;

namespace Genelib {
    class TryFeedingAnimal : CollectibleBehavior {
        public const string Code = "genelib.tryfeedinganimal";

        public TryFeedingAnimal(CollectibleObject collObj) : base(collObj) { }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling) {
            AnimalHunger animalHunger = entitySel?.Entity?.GetBehavior<AnimalHunger>();
            if (animalHunger != null) {
                animalHunger.OnInteract(byEntity, slot, null, EnumInteractMode.Interact, ref handling);
            }
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
        }
    }
}
