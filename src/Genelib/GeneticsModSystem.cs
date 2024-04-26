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

        internal static ICoreServerAPI ServerAPI { get; private set; }
        internal static ICoreClientAPI ClientAPI { get; private set; }

        // Called during intial mod loading, called before any mod receives the call to Start()
        public override void StartPre(ICoreAPI api) {
            genetics = new AssetCategory(nameof(genetics), true, EnumAppSide.Server);
        }

        // Called on server and client
        public override void Start(ICoreAPI api)
        {
            api.RegisterEntityBehaviorClass("genetics", typeof(Genetics));
            api.RegisterEntityBehaviorClass("reproduce", typeof(Reproduce));
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            List<IAsset> assets = api.Assets.GetManyInCategory(genetics.Code, "");
            foreach (IAsset asset in assets) {
                try {
                    GenomeType.Load(asset);
                }
                catch (Exception e) {
                    api.Logger.Error("Error loading genome type " + asset.Location.ToString() + ". " + e.Message + "\n" + e.StackTrace);
                }
            }
            api.Logger.Event(assets.Count + " genome types loaded");
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            ServerAPI = api;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            ClientAPI = api;
        }
    }
}
