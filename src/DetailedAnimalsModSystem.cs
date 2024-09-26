using System;
using System.Collections.Generic;

﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Common;
using Vintagestory.GameContent;

using Genelib;

namespace DetailedAnimals
{
    public class DetailedAnimalsModSystem : ModSystem
    {
        public static readonly string modid = "detailedanimals";

        public override void Start(ICoreAPI api)
        {
            GenomeType.RegisterInterpreter("Pig", new PigGenetics());
            GenomeType.RegisterInterpreter("Junglefowl", new JunglefowlGenetics());
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            // Server-side code goes here
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            // Client-side code goes here
        }
    }
}
