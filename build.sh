# To define the environment variable, put something like this in your .bashrc file:
# export VINTAGE_STORY_DEV="$HOME/software/vintagestory_dev"

RED='\033[0;31m'
NC='\033[0m' # No Color

null_textured_shapes=$(grep -rl "#null" assets/)
# Only print anything if files were found
if [[ -n $null_textured_shapes ]]; then
    echo -e "${RED}These shape files contain null textures:"
    echo -e "${null_textured_shapes}${NC}"
fi

bitconverter=$(grep -rl BitConverter src/Genelib/AnimalDatabase.cs)
if [[ -n $bitconverter ]]; then
    echo -e "${RED}Warning: avoid using BitConverter in file save/load because endianness depends on machine architecture${NC}"
fi

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
