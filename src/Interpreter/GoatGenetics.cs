using Genelib;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace DetailedAnimals {
    public class GoatGenetics : GeneInterpreter {
        public string Name => "Goat";

        void GeneInterpreter.Interpret(EntityBehaviorGenetics genetics) {
            Entity entity = genetics.entity;
            if (entity.World.Side == EnumAppSide.Client) {
                return;
            }
            Genome genome = genetics.Genome;
            if (IsPolled(genome)) {
                EntityBehaviorAntlerGrowth antlers = entity.GetBehavior<EntityBehaviorAntlerGrowth>();
                if (antlers == null) return;
                antlers.Inventory[0].Itemstack = null;
                FieldInfo field = typeof(EntityBehaviorAntlerGrowth).GetField("growDurationMonths", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) {
                    field.SetValue(antlers, -1);
                }
                else {
                    entity.Api.Logger.Warning("Unable to access EntityBehaviorAntlerGrowth.growDurationMonths for entity " +  entity.Code);
                }
            }
        }

        public static bool IsPolled(Genome genome) {
            return genome.HasAutosomal("polled", "polled");
        }
    }
}
