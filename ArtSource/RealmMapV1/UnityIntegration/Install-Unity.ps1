[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [switch]$Overwrite
)
$ErrorActionPreference = 'Stop'
$packageRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$projectRoot = [IO.Path]::GetFullPath($ProjectPath)
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'Assets') -PathType Container)) {
    throw 'ProjectPath must point to an existing Unity project containing Assets.'
}
$targetRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Assets/RealmMapV1'))
$assetsPrefix = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Assets')) + [IO.Path]::DirectorySeparatorChar
if (-not $targetRoot.StartsWith($assetsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Destination is outside the requested Unity Assets directory.'
}
$resourceRoot = Join-Path $targetRoot 'Resources/UI/RealmMapV1'
$copies = [Collections.Generic.List[object]]::new()
foreach ($folder in @('Runtime', 'Editor')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $folder) -File) {
        if ($file.Extension -notin @('.cs', '.asmdef')) { continue }
        $copies.Add([pscustomobject]@{Source=$file.FullName;Target=(Join-Path (Join-Path $targetRoot "Scripts/$folder") $file.Name)})
    }
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $packageRoot 'Assets') -Filter '*.png' -File) {
    $copies.Add([pscustomobject]@{Source=$file.FullName;Target=(Join-Path $resourceRoot $file.Name)})
}
foreach ($name in @('entrances-layout.json','ui-layout.json','lobby-support-layout.json','world-layout.json','lobby-layout.json')) {
    $sourcePath = Join-Path $packageRoot $name
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Package file missing: $name" }
    $copies.Add([pscustomobject]@{Source=$sourcePath;Target=(Join-Path $resourceRoot $name)})
}
foreach ($entry in $copies) {
    if (Test-Path -LiteralPath $entry.Target) {
        if ((Get-FileHash -LiteralPath $entry.Source).Hash -eq (Get-FileHash -LiteralPath $entry.Target).Hash) { continue }
        if (-not $Overwrite) { throw "Existing package file differs. Use -Overwrite to update only this package: $($entry.Target)" }
    }
}
foreach ($entry in $copies) {
    if ((Test-Path -LiteralPath $entry.Target) -and
        (Get-FileHash -LiteralPath $entry.Source).Hash -eq (Get-FileHash -LiteralPath $entry.Target).Hash) { continue }
    if ($PSCmdlet.ShouldProcess($entry.Target, 'Copy RealmMapV1 package file')) {
        [IO.Directory]::CreateDirectory((Split-Path -Parent $entry.Target)) | Out-Null
        Copy-Item -LiteralPath $entry.Source -Destination $entry.Target -Force
        if ((Get-FileHash -LiteralPath $entry.Source).Hash -ne (Get-FileHash -LiteralPath $entry.Target).Hash) {
            throw "Copy hash mismatch: $($entry.Target)"
        }
    }
}
Write-Output "Package target: $targetRoot"
Write-Output 'Next: wait for Unity script compilation, then use 仙途 > 秘境资源包 > 导入资源并生成组合预制体.'
Write-Output 'This installer does not open Unity, edit existing game UI, modify scenes, or contact a backend.'
