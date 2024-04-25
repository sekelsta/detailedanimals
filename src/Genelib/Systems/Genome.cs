using System;
using Genelib.Extensions;

namespace Genelib {
    public class Genome {
        protected static readonly int NUM_DIVERSITY_GENES = 32;

        public GenomeType type  { get; private set; }
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

        public Genome(GenomeType type, byte[] autosomal, byte[] anonymous, byte[] primary_xz, byte[] secondary_xz, byte[] yw) {
            this.type = type;
            this.autosomal = atLeastSize(autosomal, 2 * type.AutosomalGeneCount);
            this.anonymous = anonymous;
            this.primary_xz = atLeastSize(primary_xz, type.XZGeneCount);
            this.secondary_xz = atLeastSize(secondary_xz, type.XZGeneCount);
            this.yw = atLeastSize(yw, type.YWGeneCount);
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
            type = frequencies.ForType;
            autosomal = new byte[2 * frequencies.Autosomal.Length];
            for (int gene = 0; gene < frequencies.Autosomal.Length; ++gene) {
                autosomal[2 * gene] = getRandomAllele(frequencies.Autosomal[gene], random);
                autosomal[2 * gene + 1] = getRandomAllele(frequencies.Autosomal[gene], random);
            }

            anonymous = new byte[2 * NUM_DIVERSITY_GENES];
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
                    yw = inherit_yw(father.yw, random);
                }
                else {
                    secondary_xz = inherit_xz(father.primary_xz, father.secondary_xz, random);
                }
            }
            else {
                // Bird
                primary_xz = inherit_xz(father.primary_xz, father.secondary_xz, random);
                if (isHeterogametic) {
                    yw = inherit_yw(mother.yw, random);
                }
                else {
                    secondary_xz = inherit_xz(mother.primary_xz, mother.secondary_xz, random);
                }
            }
            inherit_autosomal(mother.autosomal, father.autosomal, random);
            inherit_autosomal(mother.anonymous, father.anonymous, random);
            return this;
        }

        protected virtual byte[] inherit_autosomal(byte[] maternal, byte[] paternal, Random random) {
            if (maternal == null || paternal == null) {
                return null;
            }
            if (maternal.Length != paternal.Length) {
                throw new ArgumentException("Parent gene arrays must match in length");
            }
            byte[] result = new byte[maternal.Length];
            for (int i = 0; i < maternal.Length / 2; ++i) {
                result[2 * i] = random.NextBool() ? maternal[2 * i] : maternal[2 * i + 1];
                result[2 * i + 1] = random.NextBool() ? paternal[2 * i] : paternal[2 * i + 1];
            }
            return result;
        }

        protected virtual byte[] inherit_xz(byte[] maternal, byte[] paternal, Random random) {
            if (maternal == null || paternal == null) {
                return null;
            }
            if (maternal.Length != paternal.Length) {
                throw new ArgumentException("Parent gene arrays must match in length");
            }
            byte[] result = new byte[maternal.Length];
            for (int i = 0; i < maternal.Length; ++i) {
                result[i] = random.NextBool() ? maternal[i] : paternal[i];
            }
            return result;
        }

        protected virtual byte[] inherit_yw(byte[] parent_yw, Random random) {
            if (parent_yw == null) {
                return null;
            }
            return (byte[]) parent_yw.Clone();
        }

        public virtual Genome Mutate(int p, Random random, AlleleSet allowed) {
            if (autosomal != null) {
                for (int gene = 0; gene < allowed.Autosomal.Length; ++gene) {
                    for (int n = 0; n < 2; ++n) {
                        if (random.NextSingle() < p) {
                            Autosomal(gene, n, allowed.Autosomal[gene][random.Next(allowed.Autosomal[gene].Length)]);
                        }
                    }
                }
            }
            if (anonymous != null) {
                for (int gene = 0; gene < anonymous.Length; ++gene) {
                    for (int n = 0; n < 2; ++n) {
                        if (random.NextSingle() < p) {
                            Anonymous(gene, n, (byte)random.Next(256));
                        }
                    }
                }
            }
            if (primary_xz != null) {
                for (int gene = 0; gene < allowed.XZ.Length; ++gene) {
                    if (random.NextSingle() < p) {
                        XZ(gene, 0, allowed.XZ[gene][random.Next(allowed.XZ[gene].Length)]);
                    }
                }
            }
            if (secondary_xz != null) {
                for (int gene = 0; gene < allowed.XZ.Length; ++gene) {
                    if (random.NextSingle() < p) {
                        XZ(gene, 1, allowed.XZ[gene][random.Next(allowed.XZ[gene].Length)]);
                    }
                }
            }
            if (yw != null) {
                for (int gene = 0; gene < allowed.YW.Length; ++gene) {
                    if (random.NextSingle() < p) {
                        YW(gene, allowed.YW[gene][random.Next(allowed.YW[gene].Length)]);
                    }
                }
            }
            return this;
        }

        public override string ToString() {
            return "Genome { type:" + type.Name + " }";
        }
    }
}
