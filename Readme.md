Modifies [Vintage Story](https://www.vintagestory.at/).

This is [Truth and Beauty: Detailed Animals](https://mods.vintagestory.at/detailedanimals), a mod improving on animal husbandry, behavior, and genetics.

### Entity Behaviors and AI tasks
To add this mod's features to more animals, have them use these entity behaviors and AI tasks:

TODO: Update documentation on each added entity behavior and AI task. This mod was recently split from Genelib. Below documentation discusses the older merged version.

To get genetics to work right, you will have to add a few specific EntityBehaviors to your entity. By code, they are:

"genelib.genetics": For having genes, what a shocker. Specify "genomeType" here. Serverside only.

"genelib.age" replacing "grow": Makes baby animals keep their genes when they grow up. Main effects are serverside, and that is where you'll need to put info about how long it takes to grow up and such. Also add to the clientside to make animals visibly change size with growth, but there you don't need any data beyond the behavior code.

"genelib.reproduce" replacing "multiply": Allows the entity to create offspring who inherit genes from both parents. Again the main affects are serverside, that's where the data goes, and clientside you just need it to show the player the info text.

If your entity lays eggs, add the AI task "genelib.layegg" which will make its eggs carry genetic information (provided they are laid into a genelib:nestbox). Also add a list of layable egg types to the entity's attributes (a list with only one item is fine), or it will default to chicken eggs. Make sure the egg perish time is longer than the incubation length or the eggs will rot before chicks can hatch.

Aside from EntityBehaviors, if you have any sex-linked genes you should also add male:true/false to the entity's attributes to specify, for example, that roosters are male and hens are female. For convenience, if you leave this out it will take a guess based on the entity code + variant groups - if the whole thing contains the string "-female" it will be treated as female, otherwise as male.

Now, genelib.reproduce does not only make it so genes are passed down, but also changes the way that breeding depends on food in order to go with the nutrition system. So to make that work properly, also add:

"genelib.hunger": Causes the animal to keep track of nutrition and get hungry over time. You *must* include this in the behaviorConfigs. You then may leave it out of the regular client and server behavior lists, as custom loading code will add it in for you. Under the hood, this also triggers a replacement of "harvestable" with "genelib.harvestable".

AI task "genelib.forage", which ensures foods eaten are processed by the nutrition system
