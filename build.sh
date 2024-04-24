dotnet run --project ./Build/CakeBuild/CakeBuild.csproj -- "$@"
rm -r bin/
rm -r src/obj/
rm "$VINTAGE_STORY"/Mods/vintage_inheritance_*.zip
cp Build/Releases/vintage_inheritance_*.zip "$VINTAGE_STORY/Mods"
rm "$VINTAGE_STORY"_dev/Mods/vintage_inheritance_*.zip
cp Build/Releases/vintage_inheritance_*.zip "${VINTAGE_STORY}_dev/Mods"

