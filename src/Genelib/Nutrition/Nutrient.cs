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
            get => outer.hungerTree.GetFloat(Name + "Level");
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
                return Math.Max(0, 1 - fill * fill);
            }
        }

        public void Gain(float amount) {
            Level = Math.Clamp(Level + amount, -2 * MaxSafe, 2 * MaxSafe);
        }

        public void Consume(float amount) {
            Level = Math.Clamp(Level - Usage * amount, -2 * MaxSafe, 2 * MaxSafe);
        }
    }
}
