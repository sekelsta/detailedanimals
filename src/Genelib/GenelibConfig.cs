using System;

namespace Genelib {
    public class GenelibConfig {
        public string Units = "CUSTOMARY_METRIC";

        public float InbreedingResistance = 0.5f;

        public void MakeValid() {
            InbreedingResistance = Math.Clamp(InbreedingResistance, 0.05f, 0.9f);
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
    }
}
