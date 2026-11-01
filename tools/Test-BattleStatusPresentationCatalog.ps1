param([switch]$Export)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path (Split-Path -Parent $projectRoot) 'glconf'
$stubPath = Join-Path ([System.IO.Path]::GetTempPath()) ('BattleCatalogStubs-' + [guid]::NewGuid().ToString('N') + '.cs')

# Compile the actual runtime catalog and editor exporter. Only Unity entry points
# are stubbed; the TSV parser, source validation and classification are real code.
$stubs = @'
using System;
using System.Collections.Generic;
namespace UnityEngine {
    public sealed class TextAsset { public string text; public TextAsset(string value) { text = value; } }
    public static class Resources {
        public static string Json;
        public static int LoadCount;
        public static T Load<T>(string path) where T : class { LoadCount++; return new TextAsset(Json) as T; }
    }
    public static class JsonUtility {
        public static Func<string, Type, object> Parse;
        public static T FromJson<T>(string json) { return (T)Parse(json, typeof(T)); }
    }
    public static class Debug {
        public static List<string> Errors = new List<string>();
        public static void LogError(object value) { Errors.Add(value.ToString()); }
        public static void LogException(Exception value) { Errors.Add(value.Message); }
    }
    public static class Application { public static string dataPath; }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType value) { }
    }
}
namespace UnityEditor {
    public sealed class InitializeOnLoadAttribute : Attribute { }
    public sealed class MenuItem : Attribute { public MenuItem(string value) { } }
    public enum ImportAssetOptions { ForceUpdate }
    public enum PlayModeStateChange { ExitingEditMode }
    public static class AssetDatabase {
        public static void ImportAsset(string path, ImportAssetOptions options) { }
        public static T LoadAssetAtPath<T>(string path) where T : class { return null; }
    }
    public static class EditorApplication {
        public static bool isPlaying;
        public static event Action<PlayModeStateChange> playModeStateChanged;
        public static void Raise() { playModeStateChanged?.Invoke(PlayModeStateChange.ExitingEditMode); }
    }
}
namespace UnityEditor.Build.Reporting { public sealed class BuildReport { } }
namespace UnityEditor.Build {
    public interface IPreprocessBuildWithReport {
        int callbackOrder { get; }
        void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report);
    }
    public sealed class BuildFailedException : Exception { public BuildFailedException(string message) : base(message) { } }
}
'@

function Assert-True([bool]$Value, [string]$Message) {
    if (-not $Value) { throw $Message }
}
function Assert-Rejected([scriptblock]$Action, [string]$Message) {
    $rejected = $false
    try { $null = & $Action } catch { $rejected = $true }
    Assert-True $rejected $Message
}
function Reset-Catalog {
    $method = [XiuXian.Core.BattleStatusPresentationCatalog].GetMethod('Reset', [System.Reflection.BindingFlags]'NonPublic,Static')
    $null = $method.Invoke($null, @())
}
function New-Table([string]$Headers, [string[]]$Rows) {
    return "declarations`ns`ntypes`n$Headers`nnames`nreferences`ncomments`n`n" + ($Rows -join "`n") + "`n"
}
function Quote-Tsv([string]$Value) { return '"' + $Value.Replace('"', '""') + '"' }

try {
    [System.IO.File]::WriteAllText($stubPath, $stubs)
    Add-Type -Path @(
        (Join-Path $projectRoot 'Assets/Scripts/Core/BattleStatusPresentationCatalog.cs'),
        (Join-Path $projectRoot 'Assets/Scripts/Editor/BattleStatusPresentationCatalogBuilder.cs'),
        $stubPath
    )
    $jsonOptions = [System.Text.Json.JsonSerializerOptions]::new()
    $jsonOptions.IncludeFields = $true
    [UnityEngine.JsonUtility]::Parse = {
        param([string]$Json, [type]$TargetType)
        return [System.Text.Json.JsonSerializer]::Deserialize($Json, $TargetType, $jsonOptions)
    }.GetNewClosure()

    $statuses = New-Table "CfgID`tKind" @("101`t1", "7`t2")
    $stunEffects = Quote-Tsv '[{"typ":"battle_damage","id":0},{"typ":"battle_status","id":101}]'
    $silenceEffects = Quote-Tsv '[{"typ":"battle_status","id":7}]'
    $skills = New-Table "CfgID`tEffects" @("907`t$stunEffects", "101`t$silenceEffects")
    $json = [XiuXian.EditorTools.BattleStatusPresentationCatalogBuilder]::BuildCatalogJson($statuses, $skills)
    $data = $json | ConvertFrom-Json
    Assert-True ($data.stunStatusIds.Count -eq 1 -and $data.stunStatusIds[0] -eq 101) 'Status classification must use Kind.'
    Assert-True ($data.stunSkillIds.Count -eq 1 -and $data.stunSkillIds[0] -eq 907) 'Skill classification must follow effect references, not skill ID.'

    [UnityEngine.Resources]::Json = $json
    Reset-Catalog
    Assert-True ([XiuXian.Core.BattleStatusPresentationCatalog]::IsStunStatus('101')) 'Known stun was not classified.'
    Assert-True (-not [XiuXian.Core.BattleStatusPresentationCatalog]::IsStunStatus('7')) 'Silence was incorrectly classified as stun.'
    Assert-True ([XiuXian.Core.BattleStatusPresentationCatalog]::SkillAppliesStun('907')) 'Referenced stun skill was not classified.'
    Assert-True (-not [XiuXian.Core.BattleStatusPresentationCatalog]::SkillAppliesStun('101')) 'Skill IDs must not be treated as status IDs.'
    Assert-True (-not [XiuXian.Core.BattleStatusPresentationCatalog]::IsStunStatus('unknown')) 'Unknown IDs must remain unclassified.'
    Assert-True ([UnityEngine.Resources]::LoadCount -eq 1) 'Repeated events must not reload Resources.'

    Assert-Rejected { [XiuXian.EditorTools.BattleStatusPresentationCatalogBuilder]::BuildCatalogJson($statuses + "101`t1`n", $skills) } 'Duplicate status IDs must fail.'
    Assert-Rejected { [XiuXian.EditorTools.BattleStatusPresentationCatalogBuilder]::BuildCatalogJson($statuses, $skills.Replace('101}', '999}')) } 'Missing status references must fail.'
    Assert-Rejected { [XiuXian.EditorTools.BattleStatusPresentationCatalogBuilder]::BuildCatalogJson($statuses, $skills.Replace('battle_status', 'new_unknown_effect')) } 'Unknown effects must fail.'
    Assert-Rejected { [XiuXian.EditorTools.BattleStatusPresentationCatalogBuilder]::BuildCatalogJson($statuses, $skills + '"unterminated') } 'Malformed TSV must fail.'
    $changed = [XiuXian.EditorTools.BattleStatusPresentationCatalogBuilder]::BuildCatalogJson($statuses.Replace("101`t1", "101`t2"), $skills) | ConvertFrom-Json
    Assert-True ($changed.sourceFingerprint -ne $data.sourceFingerprint -and $changed.stunStatusIds.Count -eq 0 -and $changed.stunSkillIds.Count -eq 0) 'Changing the source must refresh both mappings and fingerprint.'

    [UnityEngine.Resources]::Json = $json.Replace('"schemaVersion": 1', '"schemaVersion": 999')
    Reset-Catalog
    Assert-True (-not [XiuXian.Core.BattleStatusPresentationCatalog]::IsStunStatus('101')) 'Invalid exports must not classify control states.'
    Assert-True ([UnityEngine.Debug]::Errors.Count -gt 0) 'Invalid exports must emit an explicit error.'

    [UnityEngine.Application]::dataPath = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString('N') + '/Client/Assets')
    $builder = [XiuXian.EditorTools.BattleStatusPresentationCatalogBuilder]::new()
    Assert-Rejected { $builder.OnPreprocessBuild($null) } 'Builds with missing sources must fail rather than ship stale data.'
    [UnityEditor.EditorApplication]::isPlaying = $true
    [UnityEditor.EditorApplication]::Raise()
    Assert-True (-not [UnityEditor.EditorApplication]::isPlaying) 'Entering Play with missing sources must be stopped.'

    $statusSource = [System.IO.File]::ReadAllText((Join-Path $sourceRoot 'BattleStatusCfg.tsv'))
    $skillSource = [System.IO.File]::ReadAllText((Join-Path $sourceRoot 'BattleSkillCfg.tsv'))
    $realJson = [XiuXian.EditorTools.BattleStatusPresentationCatalogBuilder]::BuildCatalogJson($statusSource, $skillSource)
    $assetPath = Join-Path $projectRoot 'Assets/Resources/BattleStatusPresentationCatalog.json'
    if ($Export) { [System.IO.File]::WriteAllText($assetPath, $realJson, [System.Text.UTF8Encoding]::new($false)) }
    Assert-True ([System.IO.File]::ReadAllText($assetPath) -ceq $realJson) 'Committed export is stale; rerun with -Export or use the Unity menu.'
    $realData = $realJson | ConvertFrom-Json
    Write-Output "Battle status catalog checks passed. Sources: $($realData.statusCount) statuses, $($realData.skillCount) skills; stun: $($realData.stunStatusIds.Count) statuses, $($realData.stunSkillIds.Count) skills."
    Write-Output 'Unity JSON/import and runtime visual verification still require the Unity Editor.'
}
finally {
    if (Test-Path -LiteralPath $stubPath) { Remove-Item -LiteralPath $stubPath }
}
