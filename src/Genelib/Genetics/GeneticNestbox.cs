using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Genelib {
    public class GeneticNestbox : BlockEntityDisplay, IAnimalNest {
        public AssetLocation[] suitableFor;
        public Entity occupier;
        public InventoryGeneric inventory;

        protected bool WasOccupied = false;
        protected double LastUpdateHours = -1;

        public override InventoryBase Inventory => inventory;
        public string inventoryClassName = "genelib.nestbox";
        public override string InventoryClassName => inventoryClassName;

        public Vec3d Position => Pos.ToVec3d().Add(0.5, 0.5, 0.5);
        public string Type => "nest";

        public bool IsSuitableFor(Entity entity) {
            return entity is EntityAgent && suitableFor.Any(name => entity.WildCardMatch(name));
        }

        public bool Occupied(Entity entity) {
            return occupier != null && occupier != entity;
        }

        public bool Occupied() {
            return occupier != null && occupier.Alive;
        }

        public void SetOccupier(Entity entity) {
            occupier = entity;
        }

        public float DistanceWeighting {
            get {
                int numEggs = CountEggs();
                return numEggs == 0 ? 2 : 3 / (numEggs + 2);
            }
        }

        public bool TryAddEgg(Entity entity, string chickCode, double incubationDays) {
            // TO_OPTIMIZE: Avoid looping over the inventory twice
            if (Full()) {
                return false;
            }
            ItemStack eggStack = null;
            Reproduce reproduce = entity.GetBehavior<Reproduce>();
            if (reproduce != null) {
                eggStack = reproduce.GiveEgg();
            }
            else {
                string eggCode = "game:egg-chicken-raw"; // TODO: Get correct egg type
                Item eggItem = entity.World.GetItem(new AssetLocation(eggCode));
                if (eggItem == null) {
                    entity.Api.Logger.Warning("Failed to resolve egg " + eggCode + " for entity " + entity.Code);
                    return false;
                }
                eggStack = new ItemStack(eggItem);
                if (chickCode != null) {
                    TreeAttribute chickTree = new TreeAttribute();
                    chickTree.SetString("code", AssetLocation.Create(chickCode, entity.Code.Domain).ToString());
                    chickTree.SetInt("generation", entity.WatchedAttributes.GetInt("generation", 0) + 1);
                    eggStack.Attributes["chick"] = chickTree;
                }
            }
            if (eggStack.Attributes.HasAttribute("chick")) {
                eggStack.Attributes.SetDouble("incubationHoursRemaining", incubationDays * 24);
            }
            AddEgg(entity, eggStack);
            return true;
        }

        public bool Full() {
            return CountEggs() >= inventory.Count;
        }

        public void AddEgg(Entity entity, ItemStack eggStack) {
            for (int i = 0; i < inventory.Count; ++i) {
                if (inventory[i].Empty) {
                    inventory[i].Itemstack = eggStack;
                    inventory.DidModifyItemSlot(inventory[i]);
                    return;
                }
            }
            throw new ArgumentException("Can't add egg to full nestbox!");
        }

        public int CountEggs() {
            int count = 0;
            for (int i = 0; i < inventory.Count; ++i) {
                if (!inventory[i].Empty) {
                    ++count;
                }
            }
            return count;
        }

        public override void Initialize(ICoreAPI api) {
            inventoryClassName = Block.Attributes["inventoryClassName"]?.AsString();
            int capacity = Block.Attributes["quantitySlots"]?.AsInt(1) ?? 1;
            if (inventory == null) {
                CreateInventory(capacity, api);
            }
            else if (capacity != inventory.Count) {
                api.Logger.Warning("Nestbox loaded with " + inventory.Count + " capacity when it should be " + capacity + ".");
                InventoryGeneric oldInv = inventory;
                CreateInventory(capacity, api);
                for (int i = 0; i < capacity && i < oldInv.Count; ++i) {
                    if (!oldInv[i].Empty) {
                        inventory[i].Itemstack = oldInv[i].Itemstack;
                        inventory.DidModifyItemSlot(inventory[i]);
                    }
                }
            }
            base.Initialize(api);

            if (LastUpdateHours == -1) {
                LastUpdateHours = Api.World.Calendar.TotalHours;
            }

            string[] suitable = Block.Attributes["suitableFor"]?.AsArray<string>();
            suitableFor = suitable.Select(name => AssetLocation.Create(name, Block.Code.Domain)).ToArray();

            if (api.Side == EnumAppSide.Server) {
                api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
                RegisterGameTickListener(SlowTick, 12000);
            }

            SlowTick(0);
        }

        private void CreateInventory(int capacity, ICoreAPI api) {
            inventory = new InventoryGeneric(capacity, InventoryClassName, Pos?.ToString(), api);
            inventory.Pos = this.Pos;
            inventory.SlotModified += OnSlotModified;
        }

        public override void OnBlockRemoved() {
            base.OnBlockRemoved();
            if (Api.Side == EnumAppSide.Server) {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }

        public override void OnBlockUnloaded() {
            base.OnBlockUnloaded();
            if (Api?.Side == EnumAppSide.Server) {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree) {
            base.ToTreeAttributes(tree);
            tree.SetDouble("lastUpdateHours", LastUpdateHours);
            tree.SetBool("wasOccupied", WasOccupied);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving) {
            WasOccupied = tree.GetBool("wasOccupied");
            LastUpdateHours = tree.GetDouble("lastUpdateHours");

            TreeAttribute invTree = (TreeAttribute) tree["inventory"];
            if (inventory == null) {
                int capacity = invTree.GetInt("qslots");
                CreateInventory(capacity, worldForResolving.Api);
            }
            base.FromTreeAttributes(tree, worldForResolving);
            RedrawAfterReceivingTreeAttributes(worldForResolving);
        }

        protected void SlowTick(float dt) {
            for (int i = 0; i < inventory.Count; ++i) {
                if (inventory[i].Empty) {
                    continue;
                }
                ItemStack stack = inventory[i].Itemstack;
                TreeAttribute chickData = (TreeAttribute) stack.Attributes["chick"];
                if (chickData == null) {
                    continue;
                }
                // TODO: Chance for eggs to become infertile. Should scale with month length so that longer months do not reduce fertilty.
                if (WasOccupied) {
                    double incubationHoursRemaining = stack.Attributes.GetDouble("incubationHoursRemaining", 0.0);
                    incubationHoursRemaining -= Api.World.Calendar.TotalHours - LastUpdateHours;
                    if (incubationHoursRemaining <= 0) {
                        Entity chick = Reproduce.SpawnNewborn(occupier, chickData.GetInt("generation", 0), chickData);
                        inventory[i].Itemstack = null;
                        inventory.DidModifyItemSlot(inventory[i]);
                    }
                }
            }

            LastUpdateHours = Api.World.Calendar.TotalHours;
            WasOccupied = Occupied();
        }

        public bool CanHoldItem(ItemStack stack) {
            if (stack?.Collectible.Attributes == null) {
                return false;
            }
            return stack.Collectible.Attributes["nestitem"].AsBool(false);
        }

        private void OnSlotModified(int slot) {
            Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
            MarkDirty();
        }

        public bool OnInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use)) {
                return false;
            }

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ItemStackMoveOperation op = new ItemStackMoveOperation(Api.World, EnumMouseButton.Left, 0, EnumMergePriority.AutoMerge, 1);
            if (CanHoldItem(slot.Itemstack)) {
                // Place egg in nest
                for (int i = 0; i < inventory.Count; ++i) {
                    if (inventory[i].Empty) {
                        slot.TryPutInto(inventory[i], ref op);
                        AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;
                        Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                        return true;
                    }
                }
                return false;
            }
            // Try to take all eggs from nest
            bool anyEggs = false;
            for (int i = 0; i < inventory.Count; ++i) {
                if (!inventory[i].Empty) {
                    bool onlyToPlayerInventory = false;
                    object[] transferred = byPlayer.InventoryManager.TryTransferAway(inventory[i], ref op, onlyToPlayerInventory);
                    if (transferred != null && transferred.Length > 0) {
                        anyEggs = true;
                    }
                    // If it doesn't fit, leave it in the nest
                }
            }
            if (anyEggs) {
                world.PlaySoundAt(new AssetLocation("sounds/player/collect"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
            }
            return anyEggs;
        }

        public override float GetPerishRate() {
            // Scale with month length, just like incubation time does
            return 9 / Api.World.Calendar.DaysPerMonth;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
            // No. Just don't.
        }

        protected override float[][] genTransformationMatrices() {
            ModelTransform[] transforms = Block.Attributes["displayTransforms"]?.AsArray<ModelTransform>();
            if (transforms.Length != DisplayedItems) {
                capi.Logger.Warning("Display transforms for " + Block.Code + " block entity do not match number of displayed items.");
            }

            float[][] tfMatrices = new float[transforms.Length][];
            for (int i = 0; i < transforms.Length; ++i) {
                Vec3f off = transforms[i].Translation;
                Vec3f rot = transforms[i].Rotation;
                tfMatrices[i] = new Matrixf()
                    .Translate(off.X, off.Y, off.Z)
                    .Translate(0.5f, 0, 0.5f)
                    .RotateX(rot.X * GameMath.DEG2RAD)
                    .RotateY(rot.Y * GameMath.DEG2RAD)
                    .RotateZ(rot.Z * GameMath.DEG2RAD)
                    .Translate(-0.5f, 0, -0.5f)
                    .Values;
            }
            return tfMatrices;
        }
    }
}
