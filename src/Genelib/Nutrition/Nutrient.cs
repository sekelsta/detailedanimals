using System;
using Vintagestory.API.Datastructures;

namespace Genelib {
    public struct Nutrient {
        public readonly string Name;
        public readonly float Usage;
        public readonly float MaxSafe;
        private readonly AnimalHunger outer;

        public Nutrient(string name, JsonObject typeAttributes, AnimalHunger outer) {
            this.Name = name;
            this.Usage = typeAttributes[Name].AsFloat();
            if (typeAttributes.KeyExists(Name + "Max")) {
                this.MaxSafe = typeAttributes[Name + "Max"].AsFloat();
            }
            else {
                this.MaxSafe = 2 * Usage;
            }
            this.outer = outer;
            if (MaxSafe <= 0) {
                throw new ArgumentException("Max nutrient '" + name + "' must be greater than 0");
            }
        }

        public float Level {
            // If we're on the client side, we need to re-fetch the hunger tree in case it updated, instead of trusting outer.hungerTree
            get => outer.entity.WatchedAttributes.GetTreeAttribute("hunger")?.GetFloat(Name + "Level") ?? 0;
            set {
                if (float.IsNaN(value)) {
                    throw new ArgumentException("Cannot set nutrient value to NaN");
                }
                outer.hungerTree.SetFloat(Name + "Level", value);
                outer.entity.WatchedAttributes.MarkPathDirty("hunger");
            }
        }

        public float Value {
            get {
                float fill = Level / MaxSafe;
                return 1 - fill * fill;
            }
        }

        public double ValueIfAdded(double added) {
            double fill = (Level + added) / MaxSafe;
            return 1 - fill * fill; // Allow negatives for more accurate decision-making
        }

        public void Gain(double amount) {
            Level = (float)Math.Clamp(Level + amount, -2 * MaxSafe, 2 * MaxSafe);
        }

        public void Consume(double amount) {
            float adjustment = 1 + Math.Max(0, Level) / MaxSafe;
            Level = (float)Math.Clamp(Level - Usage * adjustment * amount, -2 * MaxSafe, 2 * MaxSafe);
        }

        public string Amount {
            get {
                float fill = Level / MaxSafe;
                if (fill < -1) {
                    return "verylow";
                }
                else if (fill <= -0.5) {
                    return "low";
                }
                else if (fill <= -0.25) {
                    return "midlow";
                }
                else if (fill < 0.25) {
                    return "mid";
                }
                else if (fill < 0.5) {
                    return "midhigh";
                }
                else if (fill <= 1) {
                    return "high";
                }
                else {
                    return "veryhigh";
                }
            }
        }
    }
}
