using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace DetailedAnimals {
    public class GrassFoodSource : IAnimalFoodSource {
        private static string[] tallgrassArray = [ "none", "eaten", "veryshort", "short", "mediumshort", "medium", "tall", "verytall" ];
        private static string[] grassArray = [ "none", "verysparse", "sparse", "normal" ];
        private static string[] forestArray = [ "0", "1", "2", "3", "4", "5", "6", "7" ];
        private static Dictionary<string, int> tallgrassDict = index(tallgrassArray);
        private static Dictionary<string, int> grassDict = index(grassArray);
        private static Dictionary<string, int> forestDict = index(forestArray);

        private static Dictionary<string, int> index(string[] array) {
            Dictionary<string, int> dict = new Dictionary<string, int>();
            for (int i = 0; i < array.Length; ++i) {
                dict[array[i]] = i;
            }
            return dict;
        }

        public static Dictionary<GrazeMethod, System.Func<GrassFoodSource, Entity, float>> GrazeMethods 
            = new Dictionary<GrazeMethod, System.Func<GrassFoodSource, Entity, float>> {
                { GrazeMethod.Graze, (grass, entity) => grass.Graze(entity) },
                { GrazeMethod.NibbleGraze, (grass, entity) => grass.GrazeSelectively(entity) },
                { GrazeMethod.Root, (grass, entity) => grass.DigRoots(entity) },
        };

        protected BlockPos tallgrassPos;
        protected BlockPos soilPos;

        public GrassFoodSource(BlockPos pos) {
            this.soilPos = pos;
            this.tallgrassPos = soilPos.UpCopy();
        }

        public static float GrassDensity(Block block) {
            string coverage = block.Variant["grasscoverage"];
            if (coverage != null) {
                return grassDict[coverage] / (grassArray.Length - 1);
            }
            coverage = block.Variant["grass"];
            return coverage == null ? 0 : forestDict[coverage] / (forestArray.Length - 1);
        }

        public static GrassFoodSource SearchNear(Entity entity) {
            double dist = entity.SelectionBox.XSize / 2;
            BlockPos blockPos = entity.Pos.HorizontalAheadCopy(dist).XYZ.AsBlockPos;
            int entityY = blockPos.Y;

            List<BlockPos> candidates = new();
            float bestDensity = 0;

            for (int i = 8; i >= -8; --i) {
                blockPos.Y = entityY + i;
                Block block = entity.World.BlockAccessor.GetBlock(blockPos);
                if (block.Id == 0) {
                    // Air
                    continue;
                }
                if (block.FirstCodePart() == "tallgrass") {
                    candidates.Add(blockPos.DownCopy());
                }
                if (block.Variant["grass"] != null || block.Variant["grasscoverage"] != null) {
                    candidates.Clear();
                    candidates.Add(blockPos.Copy());
                    bestDensity = GrassDensity(block);
                    break;
                }
            }
            int X = blockPos.X;
            int Y = blockPos.Y;
            int Z = blockPos.Z;
            for (int x = X - 1; x <= X + 1; ++x) {
                blockPos.X = x;
                for (int y = Y - 1; y <= Y + 1; ++y) {
                    blockPos.Y = y;
                    for (int z = Z - 1; z <= Z + 1; ++z) {
                        blockPos.Z = z;
                        Block nextBlock = entity.World.BlockAccessor.GetBlock(blockPos);
                        float nextDensity = GrassDensity(nextBlock);
                        if (nextDensity > bestDensity) {
                            bestDensity = nextDensity;
                            candidates.Clear();
                            candidates.Add(blockPos.Copy());
                        }
                        else if (nextDensity == bestDensity) {
                            candidates.Add(blockPos.Copy());
                        }
                    }
                }
            }

            if (candidates.Count == 0) return null;
            return new GrassFoodSource(candidates[entity.World.Rand.Next(candidates.Count)]);
        }

        public Vec3d Position => tallgrassPos.ToVec3d();

        public bool IsSuitableFor(Entity entity, CreatureDiet diet) {
            return IsSuitableFor(entity, GrazeMethod.NibbleGraze);
        }

        public bool IsSuitableFor(Entity entity, GrazeMethod method) {
            Block block = entity.World.BlockAccessor.GetBlock(soilPos);
            string grassVariant = block.Variant["grass"]; // Used by forest floor
            if (grassVariant != null && grassVariant != "0") {
                return true;
            }
            string coverageVariant = block.Variant["grasscoverage"]; // Used by soil, clay, peat, and others
            if (coverageVariant != null && coverageVariant != "none") {
                return true;
            }

            string tallgrass = tallgrassVariant(entity.World.BlockAccessor);
            if (tallgrass == "none" || ((tallgrass == "eaten" || method == GrazeMethod.Root) && KeepTallgrass(block))) {
                return false;
            }

            return true;
        }

        public float ConsumeOnePortion(Entity entity) {
            AnimalHunger hunger = entity.GetBehavior<AnimalHunger>();
            GrazeMethod grazeMethod = hunger.GetGrazeMethod(entity.World.Rand);
            NutritionData nutrition = grazeMethod.Nutrition();
            float amount = 1;
            if (entity.World.Rand.NextSingle() <= hunger.EatRate) {
                amount = GrazeMethods[grazeMethod](this, entity);
            }
            hunger.Eat(nutrition, amount * hunger.EatRate, false);
            return 0;
        }

        public float DigRoots(Entity entity) {
            Block block = entity.World.BlockAccessor.GetBlock(soilPos);
            if (KeepTallgrass(block)) {
                return 0;
            }

            Block above = entity.World.BlockAccessor.GetBlock(tallgrassPos);
            if (above.FirstCodePart() == "tallgrass") {
                entity.World.BlockAccessor.SetBlock(0, tallgrassPos);
            }

            Block dirt = null;
            if (block.Variant.ContainsKey("grasscoverage")) {
                dirt = entity.World.GetBlock(block.CodeWithParts("none"));
            }
            else if (block.Variant.ContainsKey("grass")) {
                dirt = entity.World.GetBlock(block.CodeWithParts("0"));
            }
            if (dirt == null) {
                entity.Api.Logger.Error("GrassFoodSource unable to get non-grassy version of block " + block.Code);
            }
            else {
                entity.World.BlockAccessor.SetBlock(dirt.Id, soilPos);
            }

            return 0.5f;
        }

        public float GrazeSelectively(Entity entity) {
            float stages = 0;
            string tallgrass = tallgrassVariant(entity.World.BlockAccessor);
            if (tallgrass == "none") {
                stages = Sparsen(entity, 1);
            }
            else if (tallgrass == "eaten") {
                if (entity.World.Rand.NextSingle() < 0.5f) {
                    stages = Shorten(entity, 1);
                }
                else {
                    stages = Sparsen(entity, 1);
                }
            }
            else if (entity.World.Rand.NextSingle() < 15f / 16) {
                stages = Shorten(entity, 1);
            }
            else {
                stages = Sparsen(entity, 1);
            }

            return 0.25f * stages;
        }

        public float Graze(Entity entity) {
            int stagesAttempting = 2 + entity.World.Rand.Next(3);
            int stages = Shorten(entity, stagesAttempting);
            if (stages < stagesAttempting) {
                stages += Sparsen(entity, 1);
            }

            return 0.25f * stages;
        }

        protected bool KeepTallgrass(Block block) {
            // Avoid destroying tallgrass on sand where it can't grow back
            return !(block is BlockSoil || block is BlockFarmland || block is BlockForestFloor);
        }

        protected int Shorten(Entity entity, int stages) {
            Block above = entity.World.BlockAccessor.GetBlock(tallgrassPos);
            if (above.FirstCodePart() != "tallgrass") {
                return 0;
            }
            string prevName = above.Variant["tallgrass"];

            int prevHeight = tallgrassDict[prevName];
            int newHeight = Math.Max(0, prevHeight - stages);
            Block block = entity.World.BlockAccessor.GetBlock(soilPos);
            if (KeepTallgrass(block)) {
                newHeight = Math.Min(prevHeight, Math.Max(1, newHeight));
            }
            int blockId = 0; // Air
            if (newHeight > 0) {
                string newName = tallgrassArray[newHeight];
                Block newBlock = entity.World.GetBlock(above.CodeWithVariant("tallgrass", newName));
                if (newBlock == null) {
                    entity.Api.Logger.Error("GrassFoodSource unable to get shorter version of tallgrass " + above.Code + ", would have been " + above.CodeWithVariant("tallgrass", newName));
                    return 0;
                }
                blockId = newBlock.Id;
            }
            entity.World.BlockAccessor.SetBlock(blockId, tallgrassPos);
            return prevHeight - newHeight;
        }

        protected int Sparsen(Entity entity, int stages) {
            Block block = entity.World.BlockAccessor.GetBlock(soilPos);
            Dictionary<string, int> dict = grassDict;
            string[] array = grassArray;
            string prevName = block.Variant["grasscoverage"];
            if (prevName == null) {
                prevName = block.Variant["grass"];
                dict = forestDict;
                array = forestArray;
            }
            if (prevName == null) {
                return 0;
            }

            int prevDensity = dict[prevName];
            int newDensity = Math.Max(0, prevDensity - stages);
            string newName = array[newDensity];
            Block newBlock = entity.World.GetBlock(block.CodeWithParts(newName));
            if (newBlock == null) {
                entity.Api.Logger.Error("GrassFoodSource unable to get sparser version " + newName + " of grassy dirt " + block.Code);
                return 0;
            }
            entity.World.BlockAccessor.SetBlock(newBlock.Id, soilPos);
            return prevDensity - newDensity;
        }

        private string tallgrassVariant(IBlockAccessor blockAccessor) {
            Block above = blockAccessor.GetBlock(tallgrassPos);
            if (above.FirstCodePart() == "tallgrass") {
                return above.Variant["tallgrass"];
            }
            return "none";
        }

        public string Type => "food";
    }
}
