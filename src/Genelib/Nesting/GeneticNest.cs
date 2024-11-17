using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;

using Genelib.Extensions;

namespace Genelib {
    public class GeneticNest : BlockEntityDisplay, IAnimalNest {
        public AssetLocation[] suitableFor;
        public Entity occupier;
        public InventoryGeneric inventory;

        protected bool WasOccupied = false;
        protected double LastUpdateHours = -1;

        public override InventoryBase Inventory => inventory;
        public string inventoryClassName = "genelib.nest";
        public override string InventoryClassName => inventoryClassName;

        public Vec3d Position => Pos.ToVec3d().Add(0.5, 0.5, 0.5);
        public string Type => "nest";

        public GeneticNest() {
            container = new NestContainer(() => Inventory, "inventory");
        }

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

        public bool ContainsRot() {
            for (int i = 0; i < inventory.Count; ++i) {
                if (inventory[i].Empty) {
                    continue;
                }
                AssetLocation code = inventory[i].Itemstack.Collectible?.Code;
                if (code != null && code.Domain == "game" && code.Path == "rot") {
                    return true;
                }
            }
            return false;
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
                string[] eggCodes = entity.Attributes?.GetStringArray("eggCodes");
                string eggCode = eggCodes == null ? "game:egg-chicken-raw" : eggCodes[entity.World.Rand.Next(eggCodes.Length)];
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
                double incubationHoursTotal = incubationDays * 24 * GenelibSystem.AnimalGrowthTime;
                eggStack.Attributes.SetDouble("incubationHoursRemaining", incubationHoursTotal);
                eggStack.Attributes.SetDouble("incubationHoursTotal", incubationHoursTotal);
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
            throw new ArgumentException("Can't add egg to full nest!");
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
                api.Logger.Warning("Nest " + Block.Code + " loaded with " + inventory.Count + " capacity when it should be " + capacity + ".");
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

            (container as NestContainer).PerishRate = Block.Attributes["perishRate"]?.AsFloat(1) ?? 1;
            if (LastUpdateHours == -1) {
                LastUpdateHours = Api.World.Calendar.TotalHours;
            }

            string[] suitable = Block.Attributes["suitableFor"]?.AsArray<string>();
            suitableFor = suitable.Select(name => AssetLocation.Create(name, Block.Code.Domain)).ToArray();

            if (api.Side == EnumAppSide.Server) {
                api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
                RegisterGameTickListener(SlowTick, 12000);
                SlowTick(0);
            }
        }

        private void CreateInventory(int capacity, ICoreAPI api) {
            inventory = new InventoryGeneric(capacity, InventoryClassName, Pos?.ToString(), api);
            inventory.Pos = this.Pos;
            inventory.SlotModified += OnSlotModified;
        }

        public override void OnBlockRemoved() {
            base.OnBlockRemoved(); // Unregisters the tick listener
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
                string chickCode = chickData.GetString("code");
                if (chickCode == null || chickCode == "") {
                    continue;
                }
                if (WasOccupied) {
                    double incubationHoursPrev = stack.Attributes.GetDouble("incubationHoursRemaining", 0.0);
                    double incubationHoursNext = incubationHoursPrev - (Api.World.Calendar.TotalHours - LastUpdateHours);
                    double incubationHoursTotal = stack.Attributes.GetDouble("incubationHoursTotal", 0);
                    double check = 0.1;
                    if (incubationHoursTotal > 0 && 1 - (incubationHoursPrev / incubationHoursTotal) < check 
                                                 && 1 - (incubationHoursNext / incubationHoursTotal) >= check) {
                        EntityProperties spawnType = Api.World.GetEntityType(chickCode);
                        if (spawnType == null) {
                            throw new ArgumentException(Block.Code.ToString() + " attempted to incubate egg containing entity with code " + chickCode.ToString() + ", but no such entity was found.");
                        }
                        GenomeType genomeType = spawnType.GetGenomeType();
                        if (genomeType != null) {
                            Genome childGenome = new Genome(genomeType, chickData);
                            if (childGenome.EmbryonicLethal()) {
                                chickCode = null;
                                chickData.SetString("code", "");
                            }
                        }
                    }

                    if (incubationHoursNext <= 0 && chickCode != null && chickCode != "") {
                        EntityPos pos = new EntityPos().SetPos(Pos);
                        Entity chick = Reproduce.SpawnNewborn(Api.World, pos, occupier, chickData.GetInt("generation", 0), chickData);
                        inventory[i].Itemstack = null;
                        AnimalHunger hunger = chick.GetBehavior<AnimalHunger>();
                        if (hunger != null) {
                            hunger.Saturation = hunger.AdjustedMaxSaturation * AnimalHunger.FULL;
                        }
                    }
                    else {
                        stack.Attributes.SetDouble("incubationHoursRemaining", incubationHoursNext);
                    }

                    inventory.DidModifyItemSlot(inventory[i]);
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
                        AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;
                        AssetLocation itemPlaced = slot.Itemstack?.Collectible?.Code;
                        if (slot.TryPutInto(inventory[i], ref op) > 0) {
                            world.Api.Logger.Audit(byPlayer.PlayerName + " put 1x" + itemPlaced + " into " + Block.Code + " at " + Pos);
                            Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                            return true;
                        }
                    }
                }
                return false;
            }
            // Try to take all eggs from nest
            bool anyEggs = false;
            for (int i = 0; i < inventory.Count; ++i) {
                if (!inventory[i].Empty) {
                    ItemStack stack = inventory[i].TakeOut(1);
                    if (byPlayer.InventoryManager.TryGiveItemstack(stack)) {
                        anyEggs = true;
                        world.Api.Logger.Audit(byPlayer.PlayerName + " took 1x" + stack?.Collectible?.Code + " from " + Block.Code + " at " + Pos);
                    }
                    // If it doesn't fit, leave it in the nest
                }
            }
            if (anyEggs) {
                world.PlaySoundAt(new AssetLocation("sounds/player/collect"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                if (CountEggs() == 0 && Block.Attributes?["permenant"]?.AsBool(false) == true) {
                    IBlockAccessor blockAccess = Api.World.BlockAccessor;
                    blockAccess.SetBlock(0, Pos);
                }
            }
            return anyEggs;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder info) {
            // Deliberately avoid calling base method
            bool anyEggs = false;
            bool anyFertile = false;
            for (int i = 0; i < inventory.Count; ++i) {
                if (inventory[i].Empty) {
                    continue;
                }
                anyEggs = true;
                ItemStack stack = inventory[i].Itemstack;
                if (stack.Collectible.RequiresTransitionableTicking(Api.World, inventory[i].Itemstack)) {
                    info.Append(BlockEntityShelf.PerishableInfoCompact(Api, inventory[i], 0));
                }
                else {
                    info.AppendLine(inventory[i].GetStackName());
                }
                TreeAttribute chickData = (TreeAttribute) stack.Attributes["chick"];
                if (chickData != null) {
                    string chickCode = chickData.GetString("code");
                    if (chickCode == null || chickCode == "") {
                        info.AppendLine(" • " + Lang.Get("genelib:blockinfo-fertilitylost"));
                    }
                    else {
                        anyFertile = true;
                        double hours = stack.Attributes.GetDouble("incubationHoursRemaining", 0.0);
                        double days = hours / 24;
                        if (days > 1) {
                            info.AppendLine(" • " + Lang.Get("Incubation time remaining: {0:0.0} days", days));
                        }
                        else {
                            info.AppendLine(" • " + Lang.Get("Incubation time remaining: {0:0.0} hours", hours));
                        }
                    }
                }
            }
            if (anyEggs) {
                if (anyFertile && !WasOccupied && Full()) {
                    info.AppendLine(Lang.Get("A broody hen is needed!"));
                }
                else if (!anyFertile) {
                    info.AppendLine(Lang.Get("No eggs are fertilized"));
                }
                return;
            }
            bool showSpecies = Block.Attributes?["showSuitableSpecies"]?.AsBool(true) ?? true;
            if (showSpecies) {
                // TO_OPTIMIZE: O(entity types * suitable wildcards)
                HashSet<string> creatureNames = new HashSet<string>();
                foreach (EntityProperties type in Api.World.EntityTypes) {
                    foreach (AssetLocation suitable in suitableFor) {
                        if (RegistryObjectType.WildCardMatch(suitable, type.Code)) {
                            string code = type.Attributes?["handbook"]["groupcode"].AsString() ?? type.Code.Domain + ":item-creature-" + type.Code.Path; 
                            creatureNames.Add(Lang.Get(code));
                        }
                    }
                }
                info.AppendLine(Lang.Get("genelib:blockinfo-suitable-nestbox", string.Join(", ", creatureNames)));
            }
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
