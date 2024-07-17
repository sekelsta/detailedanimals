using Newtonsoft.Json.Linq;

using System;
using System.Text;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
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
            patchEntity(api, "game:entities/land/wolf-pup.json", "wolf", """[{ "code": "variants", "states": ["male-pup", "female-pup"] }]""");
        }

        public override double ExecuteOrder() => 0.15;

        private void fixAssetDomain(JValue jvalue, string domain) {
            if (jvalue == null) {
                return;
            }
            string loc = jvalue.Value<string>();
            AssetLocation asset = AssetLocation.Create(loc, domain);
            jvalue.Value = asset.ToString();
        }

        private void patchEntity(ICoreAPI api, string path, string newCode, string variants) {
            IAsset asset = api.Assets.Get(path);
            string domain = new AssetLocation(path).Domain;
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
                JObject jclient = token.Value<JObject>("client");
                if (jclient != null) {
                    fixAssetDomain(jclient.Value<JObject>("shape")?.Value<JValue>("base"), domain);
                    JObject jtextures = jclient.Value<JObject>("texture");
                    if (jtextures != null) {
                        fixAssetDomain(jtextures.Value<JValue>("base"), domain);
                        JArray alternates = jtextures.Value<JArray>("alternates");
                        if (alternates != null) {
                            foreach (JToken alternate in alternates) {
                                fixAssetDomain(alternate.Value<JValue>("base"), domain);
                            }
                        }
                    }
                }
                JObject jsounds = token.Value<JObject>("sounds");
                if (jsounds != null) {
                    foreach (JProperty sound in jsounds.Properties()) {
                        fixAssetDomain((JValue)sound.Value, domain);
                    }
                }
                // Also change domain for:
                // aiTasks/n/sound
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
