using Newtonsoft.Json.Linq;

using System;
using System.Text;

using Vintagestory.API.Common;
using Vintagestory.Common;

namespace TruthBeauty
{
    public class EntityVariantsModSystem : ModSystem
    {
        public override void AssetsLoaded(ICoreAPI api) {
            if (api.World.Side != EnumAppSide.Server) {
                return;
            }

            patchEntity(api, "game:entities/land/chicken-baby.json", "chicken", """[{ "code": "variants", "states": ["male-chick", "female-chick"] }]""");
            patchEntity(api, "game:entities/land/pig-wild-piglet.json", "pig-wild", """[{ "code": "variants", "states": ["male-piglet", "female-piglet"] }]""");
            patchEntity(api, "game:entities/land/sheep-bighorn-lamb.json", "sheep-bighorn", """[{ "code": "variants", "states": ["male-lamb", "female-lamb"] }]""");
            patchEntity(api, "game:entities/land/hooved/deer.json", "deer", """
                [
                    { "code": "type", "states": ["caribou"] },
                    { "code": "gender", "states": ["male", "female"] },
                    { "code": "age", "states": ["young"] }
                ]
            """
            );
        }

        public override double ExecuteOrder() => 0.15;

        private void patchEntity(ICoreAPI api, string path, string newCode, string variants) {
            IAsset asset = api.Assets.Get(path);
            JToken token;
            try {
                token = JToken.Parse(asset.ToText());
            }
            catch (Exception e) {
                api.Logger.Error("Error parsing json file " + path);
                api.Logger.Error(e);
                return;
            }

            try {
                token["code"] = newCode;
                token["variantgroups"] = JToken.Parse(variants);
            }
            catch (Exception e) {
                api.Logger.Error("Error modifying json file " + path);
                api.Logger.Error(e);
                return;
            }

            AssetLocation newAssetLocation = new AssetLocation(TBModSystem.modid, new AssetLocation(path).Path);
            IAsset newAsset = new Asset(newAssetLocation);
            newAsset.Data = Encoding.UTF8.GetBytes(token.ToString());
            api.Assets.Add(newAssetLocation, newAsset);
        }
    }
}
