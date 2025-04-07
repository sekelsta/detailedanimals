using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace DetailedAnimals {
    public class DictionaryTextureSource : ITexPositionSource {
        public MiniDictionary Mapping;
        public ITextureAtlasAPI Atlas;

        public TextureAtlasPosition this[string textureCode] {
            get => Atlas.Positions[Mapping[textureCode]];
        }

        public Size2i AtlasSize {
            get => Atlas.Size;
        }
    }
}
