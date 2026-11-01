using UnityEngine;
using UnityEngine.Events;

namespace XiuXian.Core
{
    /// <summary>Destination order for the lobby's six menus.</summary>
    public static class GameNavigation
    {
        public const int Character = 0, Cultivation = 1, Realm = 2, Sect = 3, Inventory = 4, SoulJade = 5;
        public static readonly string[] Labels = { "角色", "修炼", "秘境", "宗门", "背包", "魂玉" };
        public static readonly string[] Icons =
        {
            "nav_character", "nav_cultivation", "nav_realm", "nav_sect", "nav_inventory", "nav_souljade"
        };

        public static void Navigate(int destination, UnityAction close = null)
        {
            if (destination < Character || destination > SoulJade) return;
            close?.Invoke();
            var manager = UIManager.Instance;
            if (manager == null) return;
            if (destination == Character)
            {
                manager.Show(UIScreen.Character);
                return;
            }
            if (destination == Realm)
            {
                RealmMapScreen.Show();
                return;
            }
            manager.Show(UIScreen.Main);
            var main = Object.FindAnyObjectByType<MainPanel>(FindObjectsInactive.Include);
            if (main == null) return;
            if (destination == SoulJade)
            {
                (main.GetComponent<RoleSoulJadePanel>() ?? main.gameObject.AddComponent<RoleSoulJadePanel>()).Open();
                return;
            }
            var feature = destination == Cultivation ? LobbyFeaturePanel.Feature.Cultivation :
                destination == Sect ? LobbyFeaturePanel.Feature.Sect : LobbyFeaturePanel.Feature.Inventory;
            (main.GetComponent<LobbyFeaturePanel>() ?? main.gameObject.AddComponent<LobbyFeaturePanel>()).Open(feature);
        }
    }
}
