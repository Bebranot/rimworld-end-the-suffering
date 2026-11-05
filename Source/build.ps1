# Builds the mod assembly for every supported game version.
param([switch]$DevTest)
$ErrorActionPreference = 'Stop'
foreach ($v in '1.6', '1.7') {
    $extra = @()
    if ($DevTest) { $extra += '-p:DevTest=true' }
    dotnet build "$PSScriptRoot\EndTheSuffering.csproj" -c Release -v quiet -nologo "-p:RimVersion=$v" @extra
    if ($LASTEXITCODE -ne 0) { throw "Build failed for RimWorld $v" }
}
