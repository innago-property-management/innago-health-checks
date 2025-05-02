#! /usr/bin/env bash

installCount=$(dotnet tool list dotnet-outdated-tool --global --format json | jq '.data | length')
if [[ $installCount -lt 0 ]]; then
  dotnet tool install xmldocmd -g
fi 

projects=$(find . -name "*.csproj" -path "*/src/*")
for project in $projects; do 
  dir=$(dirname "$project")
  pushd "$dir" || continue 
  
  project_file=$(basename "$project")
  
  doc_file=$(xq --raw-output '.Project.PropertyGroup[] | select(.DocumentationFile != null).DocumentationFile' "$project_file" | tr '\' '/')
  doc_dir=$(dirname "$doc_file")
  
  assembly_name=$(xq --raw-output '.Project.PropertyGroup[] | select(.AssemblyName != null).AssemblyName' "$project_file")
  
  xmldocmd "$doc_dir/$assembly_name.dll" \
    --source . \
    --visibility public \
    --skip-unbrowsable \
    --skip-compiler-generated \
    --clean \
    docs/

  popd || return 
done
