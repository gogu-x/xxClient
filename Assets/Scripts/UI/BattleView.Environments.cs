using System.Collections.Generic;
using UnityEngine;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    public sealed partial class BattleView
    {
        private static readonly string[] BattleEnvironmentResources =
        {
            "bg-battle", "bg-battle-moon", "bg-battle-jade"
        };
        private static string _lastBattleEnvironmentResource;
        private readonly List<Sprite> _environmentSprites = new();
        private readonly List<string> _environmentResources = new();
        private bool _environmentsLoaded;
        private string _currentBattleEnvironmentResource;

        private void SelectBattleEnvironment(bool randomize)
        {
            CacheBattleEnvironments();
            if (_environmentSprites.Count == 0) return;
            int selected = 0;
            if (randomize && !string.IsNullOrEmpty(_lastBattleEnvironmentResource))
            {
                int previous = _environmentResources.IndexOf(_lastBattleEnvironmentResource);
                int candidates = _environmentSprites.Count - (previous >= 0 ? 1 : 0);
                if (candidates > 0)
                {
                    selected = Random.Range(0, candidates);
                    if (previous >= 0 && selected >= previous) selected++;
                }
            }
            if (randomize) _lastBattleEnvironmentResource = _environmentResources[selected];
            _landscapeBackgroundSprite = _environmentSprites[selected];
            _portraitBackgroundSprite = _landscapeBackgroundSprite;
            _currentBattleEnvironmentResource = _environmentResources[selected];
            RefreshEnvironmentLabel();
            UpdateBackgroundSprite();
            FitBattleBackground();
        }

        private void RefreshEnvironmentLabel()
        {
            if (_environmentLabel == null) return;
            _environmentLabel.text = _currentBattleEnvironmentResource switch
            {
                "bg-battle-moon" => "月下云海",
                "bg-battle-jade" => "青岚幽谷",
                _ => "云海秘境"
            };
        }

        private void CacheBattleEnvironments()
        {
            if (_environmentsLoaded) return;
            _environmentsLoaded = true;
            foreach (var resource in BattleEnvironmentResources)
            {
                var sprite = Q.Sprite(resource);
                if (sprite == null) continue;
                _environmentSprites.Add(sprite);
                _environmentResources.Add(resource);
            }
            if (_environmentSprites.Count == 0)
                Debug.LogWarning("[BattleView] QuietInk bg-battle missing; using the warm paper camera background.");
        }

        private void ReleaseBattleEnvironments()
        {
            if (_battleBackground != null) _battleBackground.sprite = null;
            _landscapeBackgroundSprite = null;
            _portraitBackgroundSprite = null;
            _environmentSprites.Clear();
            _environmentResources.Clear();
            _environmentsLoaded = false;
            // These are shared Resources sprites, not Sprite.Create instances owned by this view.
        }
    }
}
