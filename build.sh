# To define the environment variable, put something like this in your .bashrc file:
# export VINTAGE_STORY_DEV="$HOME/software/vintagestory_dev"

starttime=$(($(date +%s%N)/1000000))

RED='\033[0;31m'
NC='\033[0m' # No Color

null_textured_shapes=$(grep -rl "#null" assets/)
# Only print anything if files were found
if [[ -n $null_textured_shapes ]]; then
    echo -e "${RED}These shape files contain null textures:"
    echo -e "${null_textured_shapes}${NC}"
fi

precook=$(($(date +%s%N)/1000000))
python3 texsrc/cook.py
postcook=$(($(date +%s%N)/1000000))

cp assets/detailedanimals/lang/es-419.json assets/detailedanimals/lang/es-es.json
dotnet run --project ./Build/CakeBuild/CakeBuild.csproj -- "$@"
rm assets/detailedanimals/lang/es-es.json
rm -r bin/
rm -r src/obj/
rm "${VINTAGE_STORY_DEV}"/Mods/detailedanimals_*.zip
cp Build/Releases/detailedanimals_*.zip "${VINTAGE_STORY_DEV}/Mods"

endtime=$(($(date +%s%N)/1000000))
cooktime=$(( postcook - precook ))
buildtime=$(( endtime - postcook ))
totaltime=$(( endtime - starttime ))
echo -e "${totaltime} milliseconds total: $((precook - starttime)) to validate, ${cooktime} to cook, and ${buildtime} to build"
