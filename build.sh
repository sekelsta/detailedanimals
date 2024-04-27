cp assets/vintage_inheritance/lang/es-419.json assets/vintage_inheritance/lang/es-es.json
cp assets/genelib/lang/es-419.json assets/genelib/lang/es-es.json
dotnet run --project ./Build/CakeBuild/CakeBuild.csproj -- "$@"
rm assets/vintage_inheritance/lang/es-es.json
rm assets/genelib/lang/es-es.json
rm -r bin/
rm -r src/obj/
rm "$VINTAGE_STORY"/Mods/vintage_inheritance_*.zip
cp Build/Releases/vintage_inheritance_*.zip "$VINTAGE_STORY/Mods"
rm "$VINTAGE_STORY"_dev/Mods/vintage_inheritance_*.zip
cp Build/Releases/vintage_inheritance_*.zip "${VINTAGE_STORY}_dev/Mods"

