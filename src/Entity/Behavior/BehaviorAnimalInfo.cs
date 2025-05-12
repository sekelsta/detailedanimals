using Vintagestory.API.Common;
﻿using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace DetailedAnimals {
    public class BehaviorAnimalInfo : EntityBehaviorNameTag {
        public const string Code = "genelib.info";

        public string Note {
            get => entity.WatchedAttributes.GetTreeAttribute("nametag").GetString("note", "");
            set => entity.WatchedAttributes.GetTreeAttribute("nametag").SetString("note", value);
        }

        public BehaviorAnimalInfo(Entity entity) : base(entity) {
            if (!entity.WatchedAttributes.HasAttribute("UID")) {
                entity.WatchedAttributes.SetLong("UID", entity.EntityId);
            }
        }

        public override string GetName(ref EnumHandling handling) {
            // Unlike base method, don't set handling to prevent default
            return DisplayName;
        }
    }
}
