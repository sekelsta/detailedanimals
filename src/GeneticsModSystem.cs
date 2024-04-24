using System;
using System.Collections.Generic;

﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Common;
using Vintagestory.GameContent;

using Genelib.Extensions;

namespace Genelib
{
    public class GeneticsModSystem : ModSystem
    {
        public static AssetCategory genetics = null;

        internal static ICoreAPI API { get; private set; }

        // Called during intial mod loading, called before any mod receives the call to Start()
        public override void StartPre(ICoreAPI api) {
            genetics = new AssetCategory(nameof(genetics), true, EnumAppSide.Server);
        }

        // Called on server and client
        public override void Start(ICoreAPI api)
        {
            API = api;
            api.RegisterEntityBehaviorClass("reproduce", typeof(Reproduce));
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
