This repository contains two projects:
* Truth and Beauty, an animal overhaul mod where I plan to add animal genetics and allow for more detailed care and interaction.
* Genelib, a library other modders can use to add genetics without having to write quite so much code

## Truth and Beauty
This is heavily in progress. Will update this doc once things settle down.

## Genelib
**This is highly incomplete and I don't recommend using it yet!!**

The basic idea:
* You write a json file describing what genes and alleles exist, and how likely an animal is to have them
* Genelib handles the backend parts: Initializing and storing gene data for entities, and children inheriting genes from parents
* Genelib provides a few simple gene-based effects
* You will need to write code for more complicated effects like coat color genetics

### Genetics features
Supports Mendelian genetics, sex-linked genes for both mammals (XY) and birds (ZW), and haplodiploidy such as in bees. Genetic linkage is planned but not in yet. Each gene can have up to 256 alleles.

Not planned: Lateral gene transfer, chromosome rearrangement, polyploidy.

### GenomeType
The GenomeType is where you provide data in json format. You need one GenomeType per hybridizable group - so for example horses and donkeys should share the same GenomeType so that they can hybridize. This is usually going to end up meaning one GenomeType per genus, rather than species or family. Animals will not be able to hybridize unless they have the same GenomeType.

This should be a json file with 3 parts:
* "genes": A list of all possible genes and alleles for all combined species using this genome type
* "interpreters": A list of C# classes that figure out what the different gene variants actually mean. "Polygenes" provides the basic built-in functionality. If you write a custom gene interpreter, this is where you link it in.
* "initializers": Set up the probability that an animal starts with specific gene variants when spawned. You can have multiple different initializers, and each one should correspond to a population (as in the biology concept) such as a subspecies, landrace, or breed. You can also add environmental conditions, for example to make a white coat color more common in artic areas.

GenomeType files go in assets/\[modid]/genetics/\[mygenometype].json

Notes on setting up the gene list:

Though you should provide the genes and alleles as nice readable strings, for performance reasons Genelib will internally store them as numbers based on their ordering. So, DO NOT reorder your genes and alleles, or it will break existing save files. Renaming them is ok though.

Avoid naming any alleles "default", because the gene initializer uses that as a keyword.

When the mod is first installed, or when new genes are added, existing entities are assumed to have the _first_ allele listed. If it doesn't have any more specific name, I recommend calling the first allele "wildtype" if it is the wildtype, or otherwise calling it "standard".

You cannot have more than 256 alleles for a single gene.

### Gene Interpreters
A gene interpreter is a C# class that calculates a phenotype based on a genotype. In other words, it looks at what genes an individual animal has and figures out what that animal should look like, how much health or other stats it should have, what it drops when killed, its temperament, or anything else that you care to have genes for.

To make a gene interpreter, you should write a class implementing the GeneInterpreter interface. See its source code for info on implementing the methods. You will then need to register your gene interpreter class by calling GenomeType.RegisterInterpreter(codename, instance). The codename is the same string you'll use to refer to it from the GenomeType json file.

One interpreter is built-in, the Polygenes interpreter. This provides basic effects, currently just having fertility decrease for inbred animals. It also calculates a probabilistic COI (coefficient of inbreeding), but doesn't do anything with it except write to the entity's WatchedAttributes.

### Entity Behaviors
**Work-In-Progress Danger Zone: These are still actively being developed, and likely to change!**

To get genetics to work right, you will have to add a few specific EntityBehaviors to your entity. By code, they are:

"genelib.genetics": For having genes, what a shocker. Specify "genomeType" here.

"genelib.age" replacing "grow": Makes baby animals keep their genes when they grow up

"genelib.reproduce" replacing "multiply": Allows the entity to create offspring who inherit genes from both parents

Aside from EntityBehaviors, if you have any sex-linked genes you should also add male:true/false to the entity's attributes to specify, for example, that roosters are male and hens are female. For convenience, if you leave this out it will take a guess based on the entity code + variant groups - if the whole thing contains the string "-female" it will be treated as female, otherwise as male.
