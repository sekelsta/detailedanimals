# To define the environment variable, put something like this in your .bashrc file:
# export VINTAGE_STORY_DEV="$HOME/software/vintagestory_dev"

cp assets/truthbeauty/lang/es-419.json assets/truthbeauty/lang/es-es.json
cp assets/genelib/lang/es-419.json assets/genelib/lang/es-es.json
dotnet run --project ./Build/CakeBuild/CakeBuild.csproj -- "$@"
rm assets/truthbeauty/lang/es-es.json
rm assets/genelib/lang/es-es.json
rm -r bin/
rm -r src/obj/
rm "${VINTAGE_STORY_DEV}"/Mods/truthbeauty_*.zip
cp Build/Releases/truthbeauty_*.zip "${VINTAGE_STORY_DEV}/Mods"

