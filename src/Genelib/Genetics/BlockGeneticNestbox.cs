using Vintagestory.API.Common;

namespace Genelib {
    public class BlockGeneticNestbox : Block {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
            var blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position) as GeneticNestbox;
            if (blockEntity != null) {
                return blockEntity.OnInteract(world, byPlayer, blockSel);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
