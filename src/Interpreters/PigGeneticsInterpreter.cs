
using Vintagestory.API.Common.Entities;

namespace VintageInheritance {
    class PigGeneticsInterpreter : EntityBehavior {
        public PigGeneticsInterpreter(Entity entity) : base(entity) { }

        public override string PropertyName() => "piggenetics";

    }
}
