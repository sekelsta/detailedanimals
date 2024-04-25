
**Genelib**

Each GenomeType should generally corrspond to a genus, rather than a species or family. Animals will not be able to hybridize unless they have the same GenomeType.

When the mod is first installed, or when new genes are added, existing entities are assumed to have the _first_ allele listed. Ordering does not matter aside from which is first. If it doesn't have any more specific name, I recommend calling the first allele "wildtype" if it is the wildtype, or otherwise calling it "standard". Avoid naming any alleles "default", because the gene initializer uses that as a keyword.
