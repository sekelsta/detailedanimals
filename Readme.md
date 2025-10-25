Modifies [Vintage Story](https://www.vintagestory.at/).

This is [Truth and Beauty: Detailed Animals](https://mods.vintagestory.at/detailedanimals), a mod improving on animal husbandry, behavior, and genetics.

### Entity Behaviors and AI tasks
See genelib's documentation if you are interested in adding genetics or seasonal breeding. Nothing in this mod is strictly required for genetics except "eggreproduce" and "layegg" for birds, but everything is compatible with it.

To add this mod's features to more animals, have them use these entity behaviors and AI tasks:

"agegradually" replacing "grow": Makes animals grow in size gradually with age. Main effects are serverside, and that is where you'll need to put info about how long it takes to grow up and such. Also add to the clientside to make animals visibly change size with growth, but there you don't need any data beyond the behavior code.

If your entity lays eggs, add the AI task "layegg" which will make its eggs carry genetic information (provided they are laid into a genelib:nestbox). Also add a list of layable egg types to the entity's attributes (a list with only one item is fine), or it will default to chicken eggs. Make sure the egg perish time is longer than the incubation length or the eggs will rot before chicks can hatch.

"animalhunger": Causes the animal to keep track of nutrition and get hungry over time. You *must* include this in the behaviorConfigs. You then may leave it out of the regular client and server behavior lists, as custom loading code will add it in for you. Under the hood, this also triggers a replacement of "harvestable" with "detailedharvestable" as well as "multiply" or "genelib.multiply" with "reproduce".

AI task "forage", which ensures foods eaten are processed by the nutrition system

Note using the nutrition system requires ALL of the above replacements, and ALL of the above replacements require the full nutrition system.
