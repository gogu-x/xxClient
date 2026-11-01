using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace XiuXian.Core
{
    /// <summary>
    /// Only classifies presentation from the exported battle configuration. It does not
    /// infer control duration or change authoritative combat events.
    /// </summary>
    public static class BattleStatusPresentationCatalog
    {
        public const int SchemaVersion = 1;
        public const string ResourcePath = "BattleStatusPresentationCatalog";

        [Serializable]
        public sealed class CatalogData
        {
            public int schemaVersion;
            public string sourceFingerprint;
            public int statusCount;
            public int skillCount;
            public int[] stunStatusIds;
            public int[] stunSkillIds;
        }

        private static bool _loaded;
        private static readonly HashSet<int> StunStatuses = new();
        private static readonly HashSet<int> StunSkills = new();

        public static bool IsStunStatus(string statusId)
        {
            EnsureLoaded();
            return TryConfigId(statusId, out int id) && StunStatuses.Contains(id);
        }

        public static bool SkillAppliesStun(string skillId)
        {
            EnsureLoaded();
            return TryConfigId(skillId, out int id) && StunSkills.Contains(id);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _loaded = false;
            StunStatuses.Clear();
            StunSkills.Clear();
        }

        private static bool TryConfigId(string value, out int id) =>
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                var asset = Resources.Load<TextAsset>(ResourcePath);
                if (asset == null) throw new InvalidOperationException("exported resource is missing");
                var data = JsonUtility.FromJson<CatalogData>(asset.text);
                if (data == null || data.schemaVersion != SchemaVersion ||
                    string.IsNullOrEmpty(data.sourceFingerprint) || data.statusCount <= 0 ||
                    data.skillCount <= 0 || data.stunStatusIds == null || data.stunSkillIds == null)
                {
                    throw new InvalidOperationException("exported resource has an invalid schema");
                }
                foreach (int id in data.stunStatusIds)
                {
                    if (id <= 0 || !StunStatuses.Add(id))
                        throw new InvalidOperationException("invalid or duplicate stun status ID");
                }
                foreach (int id in data.stunSkillIds)
                {
                    if (id <= 0 || !StunSkills.Add(id))
                        throw new InvalidOperationException("invalid or duplicate stun skill ID");
                }
            }
            catch (Exception exception)
            {
                StunStatuses.Clear();
                StunSkills.Clear();
                Debug.LogError("[BattleStatusPresentationCatalog] " + exception.Message +
                    "; regenerate with Tools/XiuXian/更新战斗状态表现配置. Control classification is disabled.");
            }
        }
    }
}
