using System;

namespace Genelib.Extensions {
    public static class RandomExtensions {
        public static bool NextBool(this Random random) {
            return random.NextSingle() < 0.5;
        }
    }
}
