using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Genelib {
    public class GrassFoodSource : IAnimalFoodSource {
        private static string[] tallgrassArray = new string[] { "none", "eaten", "veryshort", "short", "mediumshort", "medium", "tall", "verytall" };
        private static string[] grassArray = new string[] { "none", "verysparse", "sparse", "normal" };
        private static string[] forestArray = new string[] { "0", "1", "2", "3", "4", "5", "6", "7" };
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

        public static Dictionary<string, System.Func<GrassFoodSource, Entity, Tuple<NutritionData, float>>> GrazeMethods 
            = new Dictionary<string, System.Func<GrassFoodSource, Entity, Tuple<NutritionData, float>>> {
                {"graze", (grass, entity) => grass.Graze(entity)},
                {"nibblegraze", (grass, entity) => grass.GrazeSelectively(entity)},
                {"root", (grass, entity) => grass.DigRoots(entity)},
        };

        protected BlockPos tallgrassPos;
        protected BlockPos soilPos;

        public GrassFoodSource(BlockPos pos) {
            this.soilPos = pos;
            this.tallgrassPos = soilPos.UpCopy();
        }

        public static GrassFoodSource SearchNear(Entity entity) {
            double dist = entity.SelectionBox.XSize / 2;
            BlockPos blockPos = entity.ServerPos.HorizontalAheadCopy(dist).XYZ.AsBlockPos;
            BlockPos columnSearchPos = blockPos.Copy();
            for (int i = 8; i >= -8; --i) {
                columnSearchPos.Y = blockPos.Y + i;
                Block block = entity.World.BlockAccessor.GetBlock(columnSearchPos);
                if (block.Id == 0) {
                    // Air
                    continue;
                }
                if (block.FirstCodePart() == "tallgrass") {
                    return new GrassFoodSource(columnSearchPos.Down());
                }
                if (block.Variant["grass"] != null || block.Variant["grasscoverage"] != null) {
                    return new GrassFoodSource(columnSearchPos);
                }
            }
            return new GrassFoodSource(blockPos);
        }

        public Vec3d Position => tallgrassPos.ToVec3d();

        public bool IsSuitableFor(Entity entity, CreatureDiet diet) {
            // Ignore whether entity diet allows grass - that's the entity's problem

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
            if (tallgrass == "none" || (tallgrass == "eaten" && KeepTallgrass(block))) {
                return false;
            }

            return true;
        }

        public float ConsumeOnePortion(Entity entity) {
            AnimalHunger hunger = entity.GetBehavior<AnimalHunger>();
            string grazeMethod = hunger.GetGrazeMethod(entity.World.Rand);
            Tuple<NutritionData, float> result = GrazeMethods[grazeMethod](this, entity);
            if (result != null) {
                hunger.Eat(result.Item1, result.Item2);
            }
            return 0;
        }

        public Tuple<NutritionData, float> DigRoots(Entity entity) {
            Block block = entity.World.BlockAccessor.GetBlock(soilPos);
            if (KeepTallgrass(block)) {
                return null;
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

            NutritionData nutrition = NutritionData.Get("vegetable");
            return new Tuple<NutritionData, float>(nutrition, 1);
        }

        public Tuple<NutritionData, float> GrazeSelectively(Entity entity) {
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

            NutritionData nutrition = NutritionData.Get("nibbleCrop");
            return new Tuple<NutritionData, float>(nutrition, 0.25f * stages);
        }

        public Tuple<NutritionData, float> Graze(Entity entity) {
            int stagesAttempting = 2 + entity.World.Rand.Next(3);
            int stages = Shorten(entity, stagesAttempting);
            if (stages < stagesAttempting) {
                stages += Sparsen(entity, 1);
            }

            NutritionData nutrition = NutritionData.Get("grass");
            return new Tuple<NutritionData, float>(nutrition, 0.25f * stages);
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
            string newName = tallgrassArray[newHeight];
            Block newBlock = entity.World.GetBlock(above.CodeWithParts(newName));
            if (newBlock == null) {
                entity.Api.Logger.Error("GrassFoodSource unable to get shorter version of tallgrass " + above.Code);
                return 0;
            }
            entity.World.BlockAccessor.SetBlock(newBlock.Id, tallgrassPos);
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
