using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using XiuXian.Core;

namespace XiuXian.EditorTools
{
    /// <summary>
    /// Exports the presentation subset of the server's source tables. Runtime only
    /// loads the committed Resources asset, never the sibling glconf directory.
    /// </summary>
    [InitializeOnLoad]
    public sealed class BattleStatusPresentationCatalogBuilder : IPreprocessBuildWithReport
    {
        public const string AssetPath = "Assets/Resources/BattleStatusPresentationCatalog.json";
        private const string StatusTable = "BattleStatusCfg.tsv";
        private const string SkillTable = "BattleSkillCfg.tsv";
        // Matches protocol StatusType.Stun and battle/internal/statuskind.Stun.
        private const int StunKind = 1;

        [Serializable]
        public sealed class EffectList
        {
            public Effect[] effects;
        }

        [Serializable]
        public sealed class Effect
        {
            public string typ;
            public int id;
        }

        static BattleStatusPresentationCatalogBuilder()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            Rebuild();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            try { Rebuild(); }
            catch (Exception exception)
            {
                EditorApplication.isPlaying = false;
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/XiuXian/更新战斗状态表现配置")]
        public static void Rebuild()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string sourceRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "glconf"));
                string statusPath = Path.Combine(sourceRoot, StatusTable);
                string skillPath = Path.Combine(sourceRoot, SkillTable);
                if (!File.Exists(statusPath) || !File.Exists(skillPath))
                    throw new InvalidDataException("Required battle source tables are missing: " + sourceRoot);

                string json = BuildCatalogJson(File.ReadAllText(statusPath), File.ReadAllText(skillPath));
                string outputPath = Path.Combine(projectRoot, AssetPath);
                if (!File.Exists(outputPath) || File.ReadAllText(outputPath) != json)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                    File.WriteAllText(outputPath, json, new UTF8Encoding(false));
                    AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
                }
                else if (AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath) == null)
                {
                    AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
                }
            }
            catch (Exception exception)
            {
                throw new BuildFailedException("Battle presentation config export failed; refusing stale data. " +
                    exception.Message);
            }
        }

        // Public for the standalone source-table validation/export script.
        public static string BuildCatalogJson(string statusSource, string skillSource)
        {
            var statuses = ReadTable(statusSource, StatusTable, "CfgID", "Kind");
            var statusKinds = new Dictionary<int, int>();
            var stunStatusIds = new List<int>();
            foreach (var row in statuses)
            {
                int id = PositiveInt(row[0], StatusTable + " CfgID");
                int kind = PositiveInt(row[1], StatusTable + " Kind");
                if (kind > 5) throw new InvalidDataException("Unknown battle status kind: " + kind);
                if (statusKinds.ContainsKey(id)) throw new InvalidDataException("Duplicate status ID: " + id);
                statusKinds.Add(id, kind);
                if (kind == StunKind) stunStatusIds.Add(id);
            }

            var skills = ReadTable(skillSource, SkillTable, "CfgID", "Effects");
            var skillIds = new HashSet<int>();
            var stunSkillIds = new List<int>();
            foreach (var row in skills)
            {
                int id = PositiveInt(row[0], SkillTable + " CfgID");
                if (!skillIds.Add(id)) throw new InvalidDataException("Duplicate skill ID: " + id);
                EffectList parsed;
                try { parsed = JsonUtility.FromJson<EffectList>("{\"effects\":" + row[1] + "}"); }
                catch (Exception exception)
                {
                    throw new InvalidDataException("Invalid effects JSON for skill " + id, exception);
                }
                if (parsed?.effects == null || parsed.effects.Length == 0)
                    throw new InvalidDataException("Missing effects for skill " + id);
                bool appliesStun = false;
                foreach (var effect in parsed.effects)
                {
                    if (effect == null) throw new InvalidDataException("Null effect for skill " + id);
                    switch (effect.typ)
                    {
                        case "battle_damage":
                        case "battle_heal":
                            break;
                        case "battle_status":
                            if (!statusKinds.TryGetValue(effect.id, out int kind))
                                throw new InvalidDataException("Skill " + id + " references missing status " + effect.id);
                            appliesStun |= kind == StunKind;
                            break;
                        default:
                            throw new InvalidDataException("Unknown effect type for skill " + id + ": " + effect.typ);
                    }
                }
                if (appliesStun) stunSkillIds.Add(id);
            }
            stunStatusIds.Sort();
            stunSkillIds.Sort();
            string fingerprint;
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(
                    StatusTable + "\n" + statusSource + "\n" + SkillTable + "\n" + skillSource));
                fingerprint = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            return "{\n" +
                "  \"schemaVersion\": " + BattleStatusPresentationCatalog.SchemaVersion + ",\n" +
                "  \"sourceFingerprint\": \"" + fingerprint + "\",\n" +
                "  \"statusCount\": " + statusKinds.Count + ",\n" +
                "  \"skillCount\": " + skillIds.Count + ",\n" +
                "  \"stunStatusIds\": [" + JoinIds(stunStatusIds) + "],\n" +
                "  \"stunSkillIds\": [" + JoinIds(stunSkillIds) + "]\n}\n";
        }

        private static string JoinIds(List<int> values)
        {
            return string.Join(", ", values.ConvertAll(value => value.ToString(CultureInfo.InvariantCulture)));
        }

        private static int PositiveInt(string value, string field)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number <= 0)
                throw new InvalidDataException("Invalid " + field + ": " + value);
            return number;
        }

        private static List<string[]> ReadTable(string source, string tableName, string idColumn, string valueColumn)
        {
            var records = ParseTsv(source, tableName);
            if (records.Count < 8) throw new InvalidDataException(tableName + " has no configuration rows");
            // The source format reserves seven declaration/header rows before data.
            string[] headers = records[3];
            int idIndex = Array.IndexOf(headers, idColumn);
            int valueIndex = Array.IndexOf(headers, valueColumn);
            if (idIndex < 0 || valueIndex < 0)
                throw new InvalidDataException(tableName + " has incompatible field headers");
            var rows = new List<string[]>();
            for (int index = 7; index < records.Count; index++)
            {
                var record = records[index];
                if (Array.TrueForAll(record, string.IsNullOrWhiteSpace)) continue;
                if (record.Length != headers.Length)
                    throw new InvalidDataException(tableName + " row " + (index + 1) + " has an invalid column count");
                rows.Add(new[] { record[idIndex], record[valueIndex] });
            }
            if (rows.Count == 0) throw new InvalidDataException(tableName + " has no configuration rows");
            return rows;
        }

        private static List<string[]> ParseTsv(string source, string tableName)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var rows = new List<string[]>();
            var fields = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            bool quoteClosed = false;
            for (int index = 0; index < source.Length; index++)
            {
                char value = source[index];
                if (quoted)
                {
                    if (value != '"') { field.Append(value); continue; }
                    if (index + 1 < source.Length && source[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else { quoted = false; quoteClosed = true; }
                    continue;
                }
                if (value == '\t' || value == '\r' || value == '\n')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    quoteClosed = false;
                    if (value == '\t') continue;
                    if (value == '\r' && index + 1 < source.Length && source[index + 1] == '\n') index++;
                    rows.Add(fields.ToArray());
                    fields.Clear();
                    continue;
                }
                if (quoteClosed) throw new InvalidDataException(tableName + " has characters after a quoted field");
                if (value == '"')
                {
                    if (field.Length != 0) throw new InvalidDataException(tableName + " has an invalid quoted field");
                    quoted = true;
                }
                else field.Append(value);
            }
            if (quoted) throw new InvalidDataException(tableName + " has an unterminated quoted field");
            if (field.Length != 0 || fields.Count != 0 || quoteClosed)
            {
                fields.Add(field.ToString());
                rows.Add(fields.ToArray());
            }
            return rows;
        }
    }
}
