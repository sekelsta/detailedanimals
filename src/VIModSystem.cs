using System;
using System.Collections.Generic;

﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Common;
using Vintagestory.GameContent;

using Genelib;

namespace VintageInheritance
{
    public class VIModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            // Common code goes here
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            Genetics.RegisterInterpreter("Pig", PigGenetics.Interpret);
            Genetics.RegisterFinalizer("Pig", PigGenetics.Finalize);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            // Client-side code goes here
        }
    }
}
