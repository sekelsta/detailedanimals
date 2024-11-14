using Genelib;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace DetailedAnimals {
    public class GoatGenetics : GeneInterpreter {
        void GeneInterpreter.Interpret(Genome genome, Entity entity) {
            if (entity.World.Side == EnumAppSide.Client) {
                return;
            }
            if (IsPolled(genome)) {
                EntityBehaviorAntlerGrowth antlers = entity.GetBehavior<EntityBehaviorAntlerGrowth>();
                antlers.Inventory[0].Itemstack = null;
                FieldInfo field = typeof(EntityBehaviorAntlerGrowth).GetField("growDurationMonths", BindingFlags.NonPublic | BindingFlags.Instance);
                if (antlers != null && field != null) {
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