using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Genelib {
    public class BlockGeneticNestbox : BlockContainer {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
            var blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position) as GeneticNestbox;
            if (blockEntity != null) {
                return blockEntity.OnInteract(world, byPlayer, blockSel);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
