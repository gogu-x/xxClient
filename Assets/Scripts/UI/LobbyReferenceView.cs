using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Q = XiuXian.Core.QuietInkUI;
using A = XiuXian.RealmMap.RealmMapArt;

namespace XiuXian.Core
{
    // Positions share the approved 941 x 1672 portrait composition.
    public static class LobbyReferenceView
    {
        public const string MarkerName = "LobbyRealmMapV1";

        public sealed class Bindings
        {
            public Button Cultivation, Battle, Inventory, MagicArtifact, More, CharacterSide,
                InventorySide, TaskSide, SectSide, ActivitySide, WelfareSide, QuestGuide,
                QuestBanner, SecretPortal, Secret, Sect, Partner, SideTask, SideRank, SoulJade, Equipment;
        }

        public static Bindings Build(RectTransform root)
        {
            var controller = root.GetComponent<LobbyBuildingController>();
            if (controller != null) controller.enabled = false;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Object.Destroy(child);
                else Object.DestroyImmediate(child);
            }

            var background = root.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = null;
                background.color = Q.Paper;
            }

            var page = Q.Design(root, MarkerName);
            A.Art(page, "lobby-background", 0, 0, 941, 1672, false);
            var hero = A.Art(page, "lobby-hero-seated", 115, 640, 475, 420);
            hero.name = "LobbyHero";

            var profile = Q.Rect(page, "ProfileArtwork", 0, 0, 720, 200);
            var portraitClip = Q.Rect(profile, "PortraitClip", 46, 46, 142, 142);
            var mask = portraitClip.gameObject.AddComponent<LobbyPortraitMask>();
            mask.raycastTarget = false;
            portraitClip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var portrait = Q.Rect(portraitClip, "portrait", 0, 0, 142, 142).gameObject.AddComponent<RawImage>();
            portrait.name = "portrait";
            portrait.raycastTarget = false;
            A.Art(profile, "avatar_frame", 40, 36, 154, 154);
            Q.Text(profile, "PlayerName", "清风道人", 224, 62, 430, 64, 42, Q.Ink, TextAlignmentOptions.Left);
            Q.Text(profile, "RealmValue", "境界待同步", 226, 128, 400, 44, 28, Q.Muted, TextAlignmentOptions.Left);
            Q.Divider(profile, 226, 122, 320);
            ButtonAt(page, "AvatarGroup", 32, 32, 640, 165);
            var more = ButtonAt(page, "More", 799, 49, 100, 100);
            for (int i = 0; i < 3; i++)
            {
                var line = Q.Divider(more.transform, 27, 29 + i * 15, 46);
                line.color = Q.Ink;
                line.rectTransform.sizeDelta = new Vector2(46, 3);
            }

            string[] sideIcons = { "activity", "welfare" };
            string[] sideNames = { "ActivitySide", "WelfareSide" };
            string[] sideLabels = { "活动", "福利" };
            for (int i = 0; i < sideNames.Length; i++)
            {
                float y = 240 + i * 158;
                var button = ButtonAt(page, sideNames[i], 772, y, 140, 145);
                A.Art(button.transform, sideIcons[i], 41, 0, 72, 72);
                Q.Text(button.transform, "Label", sideLabels[i], 10, 85, 130, 47, 29);
            }

            var questCard = A.Art(page, "info_card", 50, 1403, 841, 104, false);
            questCard.name = "QuestCard";
            questCard.color = new Color(1, 1, 1, .92f);
            Q.Text(page, "QuestTitle", "仙途指引  ›", 77, 1410, 450, 48, 32, Q.Ink, TextAlignmentOptions.Left);
            Q.Text(page, "QuestHint", "踏入秘境，寻访修行机缘", 77, 1460, 750, 34, 25, Q.Muted, TextAlignmentOptions.Left);
            ButtonAt(page, "QuestGuide", 50, 1403, 841, 104);

            var footer = Q.Rect(page, "NavigationBackground", 0, 1510, 941, 162).gameObject.AddComponent<Image>();
            footer.color = new Color(Q.Paper.r, Q.Paper.g, Q.Paper.b, .94f);
            footer.raycastTarget = false;
            Q.Divider(page, 0, 1510, 941);
            string[] icons = { "nav_character", "nav_cultivation", "nav_realm", "nav_sect", "nav_inventory", "nav_souljade" };
            string[] names = { "CharacterSide", "Cultivation", "Secret", "Sect", "Inventory", "MagicArtifact" };
            string[] labels = { "角色", "修炼", "秘境", "宗门", "背包", "魂玉" };
            for (int i = 0; i < names.Length; i++)
            {
                var button = ButtonAt(page, names[i], i * 941f / 6, 1511, 941f / 6, 161);
                A.Art(button.transform, icons[i], 44, 29, 68, 68);
                Q.Text(button.transform, "Label", labels[i], 4, 99, 941f / 6 - 8, 48, 28, Q.Ink);
            }

            RefreshCharacterSelection(page);
            return GetBindings(page);
        }

        public static Bindings GetBindings(RectTransform page)
        {
            return new Bindings
            {
                Cultivation = Find(page, "Cultivation"), Inventory = Find(page, "Inventory"),
                MagicArtifact = Find(page, "MagicArtifact"), CharacterSide = Find(page, "CharacterSide"),
                SectSide = Find(page, "SectSide"), ActivitySide = Find(page, "ActivitySide"),
                WelfareSide = Find(page, "WelfareSide"), QuestGuide = Find(page, "QuestGuide"),
                SecretPortal = Find(page, "SecretPortal"), Equipment = Find(page, "Equipment"),
                Secret = Find(page, "Secret"), Sect = Find(page, "Sect"),
                More = Find(page, "More")
            };
        }

        private static Button Find(Transform root, string name) => root?.Find(name)?.GetComponent<Button>();
        public static Button EnsureSoulJadeShortcut(RectTransform root) => Find(root, "MagicArtifact");

        public static void RefreshCharacterSelection(RectTransform root)
        {
            if (root == null) return;
            bool female = PlayerPrefs.GetInt(CharacterPanel.SelectedCharacterKey, 0) == 1;
            var displayName = root.Find("ProfileArtwork/PlayerName")?.GetComponent<TMP_Text>();
            if (displayName != null) displayName.text = female ? "云梦仙子" : "清风道人";

            var hero = root.Find("LobbyHero")?.GetComponent<Image>();
            if (hero != null)
            {
                hero.sprite = A.Sprite(female ? "lobby-hero-female" : "lobby-hero-seated");
                hero.preserveAspect = true;
            }

            var portrait = root.Find("ProfileArtwork/PortraitClip/portrait")?.GetComponent<RawImage>();
            if (portrait == null) return;
            var avatar = A.Sprite(female ? "lobby-avatar-female" : "lobby-avatar-male");
            if (avatar != null)
            {
                portrait.texture = avatar.texture;
                var rect = avatar.rect;
                portrait.uvRect = new Rect(rect.x / avatar.texture.width, rect.y / avatar.texture.height,
                    rect.width / avatar.texture.width, rect.height / avatar.texture.height);
            }
        }

        private static Button ButtonAt(Transform parent, string name, float x, float y, float w, float h)
        {
            var button = Q.Button(parent, name, "", x, y, w, h, null, null);
            button.interactable = true;
            button.transition = Selectable.Transition.None;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            return button;
        }
    }
}

