using System;
using Vintagestory.API.Common;

namespace DetailedAnimals {
    public class AnimalConfig {
        public static AnimalConfig Instance = null;

        public string Units = "CUSTOMARY_METRIC";

        public float AnimalMeat = 1.0f;
        public bool MeatScalesWithYearLength = false;

        public float InbreedingResistance = 0.6f;

        public int ConfigVersion = 1;

        public void MakeValid() {
            InbreedingResistance = Math.Clamp(InbreedingResistance, 0.05f, 0.9f);
            AnimalMeat = Math.Clamp(AnimalMeat, 0.01f, 100f);

            if (ConfigVersion >= 0 && ConfigVersion < 1) {
                ConfigVersion = 1;
                MeatScalesWithYearLength = false;
            }
        }

        public string WeightSuffix() {
            if (Units.Equals("IMPERIAL") || Units.Equals("CUSTOMARY")) {
                return "_lbs";
            }
            if (Units.Equals("METRIC")) {
                return "_kg";
            }
            if (Units.Equals("METRIC_IMPERIAL") || Units.Equals("METRIC_CUSTOMARY")) {
                return "_kg_lbs";
            }
            return "_lbs_kg";
        }

        public float MeatMultiplier() {
            float yearScale = 1;
            if (MeatScalesWithYearLength) {
                yearScale = DetailedAnimalsModSystem.API.World.Calendar.DaysPerMonth / 9;
            }
            return AnimalMeat * yearScale;
        }

        public static void Load(ICoreAPI api) {
            try {
                Instance = api.LoadModConfig<AnimalConfig>("detailedanimals.json");
            }
            catch (Exception e) {
                api.Logger.Error("Failed to load config file for Detailed Animals: " + e);
            }
            if (Instance == null) {
                Instance = new AnimalConfig();
            }
            Instance.MakeValid();
            api.StoreModConfig(Instance, "detailedanimals.json");
        }
    }
}
