Remove-Item ../../artifacts/ -Recurse -ErrorAction Ignore
Remove-Item ../../src\build-tasks\obj\ -Recurse -ErrorAction Ignore
Remove-Item ../../test\build-tasks\obj\ -Recurse -ErrorAction Ignore

function exec($_cmd) {
    write-host " > $_cmd $args" -ForegroundColor cyan
    & $_cmd @args
    if ($LASTEXITCODE -ne 0) {
        throw 'Command failed'
    }
}

exec dotnet restore ../../src/build-tasks/
exec dotnet pack -c Release ../../src/build-tasks/
exec dotnet restore 
exec dotnet msbuild /nologo /bl

if (-not (test-path .\obj\Debug\netcoreapp3.1\Apoc.contract-interface.cs)) { 
    throw 'Apoc.contract-interface.cs *not* generated'
}

echo 'Apoc.contract-interface.cs generated'