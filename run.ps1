nbgv get-version  -v NuGetPackageVersion | clip; byenow .\artifacts\ -y; dotnet pack -o artifacts
