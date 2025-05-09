using Genelib.Extensions;
using System;
using Vintagestory.API.Datastructures;

namespace Genelib {
    public class Genome {
        public GenomeType Type  { get; private set; }
        public byte[] autosomal { get; private set; }
        public byte[] anonymous { get; private set; }
        public byte[] primary_xz { get; private set; }
        public byte[] secondary_xz { get; private set; }
        public byte[] yw { get; private set; }

        public byte Autosomal(int gene, int n) {
            return autosomal[2 * gene + n];
        }

        public void Autosomal(int gene, int n, byte v) {
            autosomal[2 * gene + n] = v;
        }

        public byte Anonymous(int gene, int n) {
            return anonymous[2 * gene + n];
        }

        public void Anonymous(int gene, int n, byte v) {
            anonymous[2 * gene + n] = v;
        }

        public byte XZ(int gene, int n) {
            return n == 0 ? primary_xz[gene] : secondary_xz[gene];
        }

        public void XZ(int gene, int n, byte v) {
            if (n == 0) {
                primary_xz[gene] = v;
            }
            else {
                secondary_xz[gene] = v;
            }
        }

        public byte YW(int gene) {
            return yw[gene];
        }

        public void YW(int gene, byte v) {
            yw[gene] = v;
        }

        public bool HasAutosomal(int gene, byte v) {
            return Autosomal(gene, 0) == v || Autosomal(gene, 1) == v;
        }

        public bool HasAutosomal(int gene, byte v1, byte v2) {
            byte g1 = Autosomal(gene, 0);
            byte g2 = Autosomal(gene, 1);
            return g1 == v1 || g2 == v1 || g1 == v2 || g2 == v2;
        }

        public bool HasAutosomal(int gene, byte v1, byte v2, byte v3) {
            byte g1 = Autosomal(gene, 0);
            byte g2 = Autosomal(gene, 1);
            return g1 == v1 || g2 == v1 || g1 == v2 || g2 == v2 || g1 == v3 || g2 == v3;
        }

        public bool HasAutosomal(int gene, byte v1, byte v2, byte v3, byte v4) {
            byte g1 = Autosomal(gene, 0);
            byte g2 = Autosomal(gene, 1);
            return g1 == v1 || g2 == v1 || g1 == v2 || g2 == v2 || g1 == v3 || g2 == v3 || g1 == v4 || g2 == v4;
        }

        public bool Homozygous(int gene, byte v) {
            return Autosomal(gene, 0) == v && Autosomal(gene, 1) == v;
        }

        public bool HasXZ(int gene, byte v) {
            return (primary_xz != null && primary_xz[gene] == v) || (secondary_xz != null && secondary_xz[gene] == v);
        }

        public bool HomozygousXZ(int gene, byte v) {
            return primary_xz != null && primary_xz[gene] == v && secondary_xz != null && secondary_xz[gene] == v;
        }

        public bool Heterogametic() {
            return secondary_xz == null;
        }

        private byte[] atLeastSize(byte[] given, int size) {
            if (given != null && given.Length >= size) {
                return given;
            }
            byte[] array = new byte[size];
            if (given == null) {
                return array;
            }
            Array.Copy(given, array, given.Length);
            return array;
        }

        public Genome(AlleleFrequencies frequencies, bool heterogametic, Random random) {
            Type = frequencies.ForType;
            autosomal = new byte[2 * frequencies.Autosomal.Length];
            for (int gene = 0; gene < frequencies.Autosomal.Length; ++gene) {
                autosomal[2 * gene] = getRandomAllele(frequencies.Autosomal[gene], random);
                autosomal[2 * gene + 1] = getRandomAllele(frequencies.Autosomal[gene], random);
            }

            anonymous = new byte[2 * PolygeneInterpreter.NUM_POLYGENES];
            random.NextBytes(anonymous);

            primary_xz = new byte[frequencies.XZ.Length];
            for (int gene = 0; gene < frequencies.XZ.Length; ++gene) {
                primary_xz[gene] = getRandomAllele(frequencies.XZ[gene], random);
            }

            if (heterogametic) {
                yw = new byte[frequencies.YW.Length];
                for (int gene = 0; gene < frequencies.YW.Length; ++gene) {
                    yw[gene] = getRandomAllele(frequencies.XZ[gene], random);
                }
            }
            else {
                secondary_xz = new byte[primary_xz.Length];
                for (int gene = 0; gene < frequencies.XZ.Length; ++gene) {
                    secondary_xz[gene] = getRandomAllele(frequencies.XZ[gene], random);
                }
            }
        }

        public Genome(Genome mother, Genome father, bool heterogametic, Random random) {
            this.Type = mother.Type;
            Inherit(mother, father, heterogametic, random);
        }

        private byte getRandomAllele(float[] alleles, Random random) {
            if (alleles == null) {
                return 0;
            }
            float f = random.NextSingle();
            byte a = 0;
            for ( ; a < alleles.Length && alleles[a] < f; ++a);
            return a;
        }

        protected virtual Genome Inherit(Genome mother, Genome father, bool isHeterogametic, Random random) {
            if (father.secondary_xz == null) {
                // Mammal
                primary_xz = inherit_xz(mother.primary_xz, mother.secondary_xz, random);
                if (isHeterogametic) {
                    yw = (byte[]) father.yw.Clone();
                }
                else {
                    secondary_xz = (byte[]) father.primary_xz.Clone();
                }
            }
            else {
                // Bird
                primary_xz = inherit_xz(father.primary_xz, father.secondary_xz, random);
                if (isHeterogametic) {
                    yw = (byte[]) mother.yw.Clone();
                }
                else {
                    secondary_xz = (byte[]) mother.primary_xz.Clone();
                }
            }
            autosomal = inherit_autosomal(mother.autosomal, father.autosomal, random);
            anonymous = inherit_autosomal(mother.anonymous, father.anonymous, random);
            return this;
        }

        protected virtual byte[] inherit_autosomal(byte[] maternal, byte[] paternal, Random random) {
            if (maternal == null && paternal == null) {
                return null;
            }
            if (maternal == null || paternal == null) {
                throw new ArgumentException("Parent autosomal gene arrays should either both be null or both be non-null");
            }
            // If lengths do not match, assume the world used to use a newer version of the mod but now uses an older version
            int length = Math.Min(maternal.Length, paternal.Length);
            byte[] result = new byte[length];
            for (int i = 0; i < length / 2; ++i) {
                result[2 * i] = random.NextBool() ? maternal[2 * i] : maternal[2 * i + 1];
                result[2 * i + 1] = random.NextBool() ? paternal[2 * i] : paternal[2 * i + 1];
            }
            return result;
        }

        protected virtual byte[] inherit_xz(byte[] maternal, byte[] paternal, Random random) {
            if (maternal == null && paternal == null) {
                return null;
            }
            if (maternal == null || paternal == null) {
                throw new ArgumentException("Parent autosomal gene arrays should either both be null or both be non-null");
            }
            int length = Math.Min(maternal.Length, paternal.Length);
            byte[] result = new byte[length];
            for (int i = 0; i < length; ++i) {
                result[i] = random.NextBool() ? maternal[i] : paternal[i];
            }
            return result;
        }

        public virtual Genome Mutate(double p, Random random) {
            if (autosomal != null) {
                for (int gene = 0; gene < Type.Autosomal.GeneCount; ++gene) {
                    for (int n = 0; n < 2; ++n) {
                        if (random.NextDouble() < p) {
                            Autosomal(gene, n, (byte) random.Next(Type.Autosomal.AlleleCount(gene)));
                        }
                    }
                }
                if (Type.Autosomal.TryGetGeneID("KIT", out int KIT)) {
                    for (int n = 0; n < 2; ++n) {
                        if (random.NextDouble() < 10 * p) {
                            Autosomal(KIT, n, (byte) random.Next(Type.Autosomal.AlleleCount(KIT)));
                        }
                    }
                }
            }
            if (anonymous != null) {
                for (int gene = 0; gene < anonymous.Length; ++gene) {
                    if (random.NextDouble() < p) {
                        anonymous[gene] = (byte)random.Next(256);
                    }
                }
            }
            if (primary_xz != null) {
                for (int gene = 0; gene < Type.XZ.GeneCount; ++gene) {
                    if (random.NextDouble() < p) {
                        XZ(gene, 0, (byte) random.Next(Type.XZ.AlleleCount(gene)));
                    }
                }
            }
            if (secondary_xz != null) {
                for (int gene = 0; gene < Type.XZ.GeneCount; ++gene) {
                    if (random.NextDouble() < p) {
                        XZ(gene, 1, (byte) random.Next(Type.XZ.AlleleCount(gene)));
                    }
                }
            }
            if (yw != null) {
                for (int gene = 0; gene < Type.YW.GeneCount; ++gene) {
                    if (random.NextDouble() < p) {
                        YW(gene, (byte) random.Next(Type.YW.AlleleCount(gene)));
                    }
                }
            }
            return this;
        }

        public bool EmbryonicLethal() {
            foreach (GeneInterpreter interpreter in Type.Interpreters) {
                if (interpreter.EmbryonicLethal(this)) {
                    return true;
                }
            }
            return false;
        }

        public bool HasAutosomal(string gene, string allele) {
            int geneID = Type.Autosomal.GeneID(gene);
            return HasAutosomal(geneID, Type.Autosomal.AlleleID(geneID, allele));
        }

        public bool HasAutosomal(string gene, string allele1, string allele2) {
            int geneID = Type.Autosomal.GeneID(gene);
            return HasAutosomal(geneID, Type.Autosomal.AlleleID(geneID, allele1), Type.Autosomal.AlleleID(geneID, allele2));
        }

        public bool HasAutosomal(string gene, string allele1, string allele2, string allele3) {
            int geneID = Type.Autosomal.GeneID(gene);
            return HasAutosomal(geneID, Type.Autosomal.AlleleID(geneID, allele1), Type.Autosomal.AlleleID(geneID, allele2), Type.Autosomal.AlleleID(geneID, allele3));
        }

        public bool HasAutosomal(string gene, string allele1, string allele2, string allele3, string allele4) {
            int geneID = Type.Autosomal.GeneID(gene);
            return HasAutosomal(geneID, Type.Autosomal.AlleleID(geneID, allele1), Type.Autosomal.AlleleID(geneID, allele2), Type.Autosomal.AlleleID(geneID, allele3), Type.Autosomal.AlleleID(geneID, allele4));
        }

        public bool Homozygous(string gene, string allele) {
            int geneID = Type.Autosomal.GeneID(gene);
            return Homozygous(geneID, Type.Autosomal.AlleleID(geneID, allele));
        }

        public bool HasXZ(string gene, string allele) {
            int geneID = Type.XZ.GeneID(gene);
            return HasXZ(geneID, Type.XZ.AlleleID(geneID, allele));
        }

        public bool HomozygousXZ(string gene, string allele) {
            int geneID = Type.XZ.GeneID(gene);
            return HomozygousXZ(geneID, Type.XZ.AlleleID(geneID, allele));
        }

        public void SetNotHomozygous(string gene, string avoidAllele, AlleleFrequencies frequencies, string fallbackAllele) {
            int geneID = Type.Autosomal.GeneID(gene);
            byte avoidID = Type.Autosomal.AlleleID(geneID, avoidAllele);
            if (Homozygous(geneID, avoidID)) {
                float[] f = frequencies.Autosomal[geneID];
                for (int i = 0; i < f.Length; ++i) {
                    if (i == avoidID) {
                        continue;
                    }
                    if (f[i] > 0) {
                        Autosomal(geneID, 0, (byte) i);
                        break;
                    }
                }
                if (Homozygous(geneID, avoidID)) {
                    Autosomal(geneID, 0, Type.Autosomal.AlleleID(geneID, fallbackAllele));
                }
            }
        }

        public Genome(GenomeType type, TreeAttribute geneticsTree) {

            this.Type = type;

            byte[] autosomal = (geneticsTree.GetAttribute("autosomal") as ByteArrayAttribute)?.value;
            byte[] anonymous = (geneticsTree.GetAttribute("anonymous") as ByteArrayAttribute)?.value;
            byte[] primary_xz = (geneticsTree.GetAttribute("primary_xz") as ByteArrayAttribute)?.value;
            byte[] secondary_xz = (geneticsTree.GetAttribute("secondary_xz") as ByteArrayAttribute)?.value;
            byte[] yw = (geneticsTree.GetAttribute("yw") as ByteArrayAttribute)?.value;
            this.autosomal = atLeastSize(autosomal, 2 * type.Autosomal.GeneCount);
            this.anonymous = atLeastSize(anonymous, 2 * type.AnonymousGeneCount);
            this.primary_xz = atLeastSize(primary_xz, type.XZ.GeneCount);
            this.secondary_xz = atLeastSize(secondary_xz, type.XZ.GeneCount);
            this.yw = atLeastSize(yw, type.YW.GeneCount);
        }

        // Caller is responsible for marking the path as dirty if necessary
        public void AddToTree(TreeAttribute geneticsTree) {
            if (autosomal == null) {
                geneticsTree.RemoveAttribute("autosomal");
            }
            else {
                geneticsTree.SetAttribute("autosomal", new ByteArrayAttribute(autosomal));
            }
            if (anonymous == null) {
                geneticsTree.RemoveAttribute("anonymous");
            }
            else {
                geneticsTree.SetAttribute("anonymous", new ByteArrayAttribute(anonymous));
            }
            if (primary_xz == null) {
                geneticsTree.RemoveAttribute("primary_xz");
            }
            else {
                geneticsTree.SetAttribute("primary_xz", new ByteArrayAttribute(primary_xz));
            }
            if (secondary_xz == null) {
                geneticsTree.RemoveAttribute("secondary_xz");
            }
            else {
                geneticsTree.SetAttribute("secondary_xz", new ByteArrayAttribute(secondary_xz));
            }
            if (yw == null) {
                geneticsTree.RemoveAttribute("yw");
            }
            else {
                geneticsTree.SetAttribute("yw", new ByteArrayAttribute(yw));
            }
        }

        public override string ToString() {
            return "Genome << type:" + Type.Name 
                + ",\n    autosomal=" + ArrayToString(autosomal) 
                + ",\n    primary_xz=" + ArrayToString(primary_xz) 
                + ",\n    secondary_xz=" + ArrayToString(secondary_xz) 
                + ",\n    yw=" + ArrayToString(yw) 
                + ",\n    anonymous=" + ArrayToString(anonymous) + " >>";
        }

        private string ArrayToString<T>(T[] array) {
            if (array == null) {
                return "null";
            }
            return "[" + string.Join(",", array) + "]";
        }
    }
}
