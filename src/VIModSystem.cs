using System;
using System.Collections.Generic;

﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace VintageInheritance
{
    public class VIModSystem : ModSystem
    {
        // Called on server and client
        public override void Start(ICoreAPI api)
        {
            api.RegisterEntityBehaviorClass("piggenetics", typeof(PigGeneticsInterpreter));
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
