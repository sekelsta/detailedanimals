using System.Collections.Generic;

namespace Genelib {
    public enum SexDetermination {
        NoSexChromosomes,
        XY,
        ZW,
        XO,
        ZO,
        Haplodiploid,
        ReverseHaplodiploid
    }

    public static class SexDeterminationExtensions {
        private static Dictionary<string, SexDetermination> names;

        public static bool Heterogametic(this SexDetermination d, bool male) {
            return (male && (d == SexDetermination.XY || d == SexDetermination.XO || d == SexDetermination.ReverseHaplodiploid))
                || (!male && (d == SexDetermination.ZW || d == SexDetermination.ZO || d == SexDetermination.Haplodiploid));
        }

        public static SexDetermination Parse(string s) {
            if (names == null) {
                names = new Dictionary<string, SexDetermination>();
                names["no_sex_chromosomes"] = SexDetermination.NoSexChromosomes;
                names["genderless"] = SexDetermination.NoSexChromosomes;
                names["nongenetic"] = SexDetermination.NoSexChromosomes;
                names["hermaphrodite"] = SexDetermination.NoSexChromosomes;
                names["xy"] = SexDetermination.XY;
                names["XY"] = SexDetermination.XY;
                names["mammal"] = SexDetermination.XY;
                names["zw"] = SexDetermination.ZW;
                names["ZW"] = SexDetermination.ZW;
                names["bird"] = SexDetermination.ZW;
                names["xo"] = SexDetermination.XO;
                names["XO"] = SexDetermination.XO;
                names["zo"] = SexDetermination.ZO;
                names["ZO"] = SexDetermination.ZO;
                names["haplodiploid"] = SexDetermination.Haplodiploid;
                names["bee"] = SexDetermination.Haplodiploid;
                names["reverse_haplodiploid"] = SexDetermination.ReverseHaplodiploid;
            }
            return names[s];
        }
    }
}
