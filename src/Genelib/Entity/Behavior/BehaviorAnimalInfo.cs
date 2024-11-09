using Vintagestory.API.Common;
﻿using Vintagestory.API.Common.Entities;

namespace Genelib {
    public class BehaviorAnimalInfo : EntityBehaviorNameTag {
        public const string Code = "genelib.info";

        public string Note {
            get => entity.WatchedAttributes.GetTreeAttribute("nametag").GetString("note", "");
            set => entity.WatchedAttributes.GetTreeAttribute("nametag").SetString("note", value);
        }

        public BehaviorAnimalInfo(Entity entity) : base(entity) { }
    }
}
