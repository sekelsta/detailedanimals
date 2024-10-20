using System;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common.Entities;

namespace Genelib {
    public class GeneInitializer {
        private GenomeType type;
        private JsonObject attributes;
        private AlleleFrequencies frequencies;
        private ClimateSpawnCondition climateCondition;
        private float minGeo;
        private float maxGeo = 1;
        private float minFertility;
        private float maxFertility = 1;
        private float maxForestOrShrubs = 1;

        public AlleleFrequencies Frequencies {
            get {
                if (frequencies == null) {
                    frequencies = new AlleleFrequencies(type, attributes);
                    attributes = null;
                }
                return frequencies;
            }
        }

        public GeneInitializer(GenomeType type, JsonObject attributes) {
            this.type = type;
            this.attributes = attributes;
            if (attributes.KeyExists("conditions")) {
                JsonObject conditions = attributes["conditions"];
                climateCondition = conditions.AsObject<ClimateSpawnCondition>();
                if (conditions.KeyExists("minGeologicActivity")) {
                    minGeo = conditions["minGeologicActivity"].AsFloat();
                }
                if (conditions.KeyExists("maxGeologicActivity")) {
                    maxGeo = conditions["maxGeologicActivity"].AsFloat();
                }
                if (conditions.KeyExists("minFertility")) {
                    minFertility = conditions["minFertility"].AsFloat();
                }
                if (conditions.KeyExists("maxFertility")) {
                    maxFertility = conditions["maxFertility"].AsFloat();
                }
                if (conditions.KeyExists("maxForestOrShrubs")) {
                    maxForestOrShrubs = conditions["maxForestOrShrubs"].AsFloat();
                }
            }
        }

        public bool CanSpawnAt(ClimateCondition climate, int y) {
            if (climateCondition == null) {
                return true;
            }
            bool forestOrShrubs = (climateCondition.MinForestOrShrubs <= climate.ForestDensity 
                    || climateCondition.MinForestOrShrubs <= climate.ShrubDensity)
                && (maxForestOrShrubs >= climate.ForestDensity 
                    || maxForestOrShrubs >= climate.ShrubDensity);
            int sealevel = GenelibSystem.ServerAPI.World.SeaLevel;
            int maxheight = GenelibSystem.ServerAPI.WorldManager.MapSizeY;
            float highY = (y + 3f) > sealevel ? (y + 3f - sealevel) / (maxheight - sealevel) : (y + 3f) / sealevel;
            float lowY = (y - 3f) > sealevel ? (y - 3f - sealevel) / (maxheight - sealevel) : (y - 3f) / sealevel;
            return forestOrShrubs
                && climateCondition.MinTemp <= climate.WorldGenTemperature
                && climateCondition.MaxTemp >= climate.WorldGenTemperature
                && climateCondition.MinRain <= climate.WorldgenRainfall
                && climateCondition.MaxRain >= climate.WorldgenRainfall
                && climateCondition.MinForest <= climate.ForestDensity 
                && climateCondition.MaxForest >= climate.ForestDensity
                && climateCondition.MinShrubs <= climate.ShrubDensity
                && climateCondition.MaxShrubs >= climate.ShrubDensity
                && climateCondition.MinY <= highY
                && climateCondition.MaxY >= lowY
                && minGeo <= climate.GeologicActivity
                && maxGeo >= climate.GeologicActivity
                && minFertility <= climate.Fertility
                && maxFertility >= climate.Fertility;

        }
    }
}
