using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Genelib {
    public class Reproduce : EntityBehavior {
        public Reproduce(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject attributes) {
        }

        public override void GetInfoText(StringBuilder infotext) {
            base.GetInfoText(infotext);
            infotext.AppendLine("EntityBehavior Reproduce");
        }

        public override string PropertyName() => "reproduce";
    }
}
