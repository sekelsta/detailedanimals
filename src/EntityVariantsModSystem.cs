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
            patchEntity(api, "game:entities/land/wolf-pup.json", "wolf", """[{ "code": "variants", "states": ["male-pup", "female-pup"] }]""");
            patchEntity(api, "game:entities/land/fox.json", "fox", """
                [
                    { "code": "gender", "states": ["male", "female"] },
                    { "code": "age", "states": ["pup"] },
                    { "code": "type", "states": ["red", "arctic"] },
                ]
            """
            );
            patchEntity(api, "game:entities/land/raccoon-pup.json", "raccoon", """[{ "code": "variants", "states": ["male-pup", "female-pup"] }]""");
            patchEntity(api, "game:entities/land/hyena-pup.json", "hyena", """[{ "code": "variants", "states": ["male-pup", "female-pup"] }]""");
            patchEntity(api, "game:entities/land/gazelle.json", "gazelle", """[{ "code": "variants", "states": ["male-calf", "female-calf"] }]""");
            patchEntity(api, "game:entities/land/hare-baby.json", "hare", """[{ "code": "variants", "states": ["male-baby", "female-baby"] }]""");
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

        private void fixAssetCheckTyped(JObject parent, string key, string domain) {
            fixAssetDomain(parent.Value<JValue>(key), domain);
            JObject byType = parent.Value<JObject>(key + "ByType");
            if (byType != null) {
                foreach (JProperty type in byType.Properties()) {
                    fixAssetDomain((JValue)type.Value, domain);
                }
            }
        }

        private void fixAssetArray(JArray jarray, string domain) {
            if (jarray == null) {
                return;
            }
            for (int i = 0; i < jarray.Count; ++i) {
                fixAssetDomain((JValue)jarray[i], domain);
            }
        }

        private void fixTextures(JObject jtextures, string domain) {
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
                fixAssetDomain(token.Value<JObject>("attributes")?.Value<JValue>("killedByInfoText"), domain);
                JObject jclient = token.Value<JObject>("client");
                if (jclient != null) {
                    fixAssetDomain(jclient.Value<JObject>("shape")?.Value<JValue>("base"), domain);
                    JObject jtextures = jclient.Value<JObject>("texture");
                    fixTextures(jtextures, domain);
                    JObject jtexturesByType = jclient.Value<JObject>("textureByType");
                    if (jtexturesByType != null) {
                        foreach (JProperty textureType in jtexturesByType.Properties()) {
                            fixTextures((JObject)textureType.Value, domain);
                        }
                    }
                }
                JObject jserver = token.Value<JObject>("server");
                if (jserver != null) {
                    JObject jspawns = jserver.Value<JObject>("spawnconditions");
                    if (jspawns != null) {
                        fixAssetArray(jspawns.Value<JObject>("runtime")?.Value<JArray>("insideBlockCodes"), domain);
                        fixAssetArray(jspawns.Value<JObject>("worldgen")?.Value<JArray>("insideBlockCodes"), domain);
                    }
                    JArray jbehaviors = jserver.Value<JArray>("behaviors");
                    if (jbehaviors != null) {
                        foreach (JObject behavior in jbehaviors) {
                            fixAssetCheckTyped(behavior, "decayedBlock", domain);

                            JValue jcode = behavior.Value<JValue>("code");
                            string code = jcode.Value<string>();
                            if (code == "taskai") {
                                foreach (JObject task in behavior.Value<JArray>("aitasks")) {
                                    fixAssetCheckTyped(task, "sound", domain);
                                    fixAssetCheckTyped(task, "eatSound", domain);
                                    // entityCode, entityCodes, and stopOnNearbyEntityCodes already default to game domain
                                }
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
                JObject jsoundsByType = token.Value<JObject>("soundsByType");
                if (jsoundsByType != null) {
                    foreach (JProperty type in jsoundsByType.Properties()) {
                        JObject sounds = (JObject)type.Value;
                        foreach (JProperty sound in sounds.Properties()) {
                            fixAssetDomain((JValue)sound.Value, domain);
                        }
                    }
                }
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
