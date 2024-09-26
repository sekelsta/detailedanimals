# To define the environment variable, put something like this in your .bashrc file:
# export VINTAGE_STORY_DEV="$HOME/software/vintagestory_dev"

python3 texsrc/cook.py
cp assets/detailedanimals/lang/es-419.json assets/detailedanimals/lang/es-es.json
cp assets/genelib/lang/es-419.json assets/genelib/lang/es-es.json
dotnet run --project ./Build/CakeBuild/CakeBuild.csproj -- "$@"
rm assets/detailedanimals/lang/es-es.json
rm assets/genelib/lang/es-es.json
rm -r bin/
rm -r src/obj/
rm "${VINTAGE_STORY_DEV}"/Mods/detailedanimals_*.zip
cp Build/Releases/detailedanimals_*.zip "${VINTAGE_STORY_DEV}/Mods"

