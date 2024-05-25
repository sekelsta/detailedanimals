using System;
using Vintagestory.API.Datastructures;

namespace Genelib {
    public class Nutrient {
        public readonly string Name;
        public readonly float Usage;
        public readonly float Max;
        private readonly AnimalHunger outer;

        public Nutrient(string name, JsonObject typeAttributes, AnimalHunger outer) {
            this.Name = name;
            this.Usage = typeAttributes[Name].AsFloat();
            if (typeAttributes.KeyExists(Name + "Max")) {
                this.Max = typeAttributes[Name + "Max"].AsFloat();
            }
            else {
                this.Max = 2 * Usage;
            }
            this.outer = outer;
        }

        public float Level {
            get => outer.hungerTree.GetFloat(Name + "Level");
            set {
                outer.hungerTree.SetFloat(Name + "Level", value);
                outer.entity.WatchedAttributes.MarkPathDirty("hunger");
            }
        }

        public float Fill {
            get => Level / Max;
        }

        public void Consume(float amount) {
            Level = Math.Max(0, Level - Usage * amount);
        }
    }
}
