using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Genelib {
    public class BlockGeneticNestbox : Block {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
            var blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position) as GeneticNestbox;
            if (blockEntity != null) {
                return blockEntity.OnInteract(world, byPlayer, blockSel);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        // Override BlockContainer.OnPickBlock to go back to behavior of Block.OnPickBlock
        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            EnumHandling handled = EnumHandling.PassThrough;
            ItemStack stack = null;

            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                EnumHandling bhHandled = EnumHandling.PassThrough;
                ItemStack bhstack = behavior.OnPickBlock(world, pos, ref bhHandled);

                if (bhHandled != EnumHandling.PassThrough)
                {
                    stack = bhstack;
                    handled = bhHandled;
                }

                if (handled == EnumHandling.PreventSubsequent) return stack;
            }

            if (handled == EnumHandling.PreventDefault) return stack;

            return new ItemStack(this, 1);
        }

        // Override BlockContainer.OnBlockBroken to go back to behavior of Block.OnBlockBroken
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            bool preventDefault = false;
            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;

                behavior.OnBlockBroken(world, pos, byPlayer, ref handled);
                if (handled == EnumHandling.PreventDefault) preventDefault = true;
                if (handled == EnumHandling.PreventSubsequent) return;
            }

            if (preventDefault) return;

            if (EntityClass != null)
            {
                BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);
                if (entity != null)
                {
                    entity.OnBlockBroken(byPlayer);
                }
            }

            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] drops = GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

                if (drops != null)
                {
                    for (int i = 0; i < drops.Length; i++)
                    {
                        if (SplitDropStacks)
                        {
                            for (int k = 0; k < drops[i].StackSize; k++)
                            {
                                ItemStack stack = drops[i].Clone();
                                stack.StackSize = 1;
                                world.SpawnItemEntity(stack, new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
                            }
                        } else
                        {
                            world.SpawnItemEntity(drops[i].Clone(), new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
                        }

                    }
                }

                world.PlaySoundAt(Sounds?.GetBreakSound(byPlayer), pos.X, pos.Y, pos.Z, byPlayer);
            }

            SpawnBlockBrokenParticles(pos);
            world.BlockAccessor.SetBlock(0, pos);
        }
    }
}
