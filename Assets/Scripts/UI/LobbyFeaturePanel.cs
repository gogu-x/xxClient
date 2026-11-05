using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    /// <summary>Portrait service pages. Unavailable data stays empty; navigation remains usable.</summary>
    public sealed partial class LobbyFeaturePanel : MonoBehaviour
    {
        public enum Feature { Cultivation, Inventory, Task, Sect, Activity, Welfare, More, Partner, Rank, Estate, Settings }
        private GameObject page;
        private Feature current;
        private int category;
        private UnityAction onClose;

        private void Awake()
        {
            AudioListener.volume = PlayerPrefs.GetFloat("UI.MasterVolume", 1f);
            if (PlayerPrefs.HasKey("UI.Quality") && QualitySettings.names.Length > 0)
                QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt("UI.Quality"), 0, QualitySettings.names.Length - 1));
        }
        public void Open(Feature feature) => OpenWithClose(feature, null);
        public void OpenWithClose(Feature feature, UnityAction closed)
        {
            current = feature; category = 0; onClose = closed; Rebuild();
        }
        public void Close()
        {
            SaveCultivation();
            if (page != null) page.SetActive(false);
            var callback = onClose; onClose = null; callback?.Invoke();
        }
        private void OnDisable()
        {
            SaveCultivation();
            // A top-level screen change must not reopen the previous service overlay.
            if (page != null) page.SetActive(false);
            onClose = null;
        }
        private void ChangeFeature(Feature feature) { current = feature; category = 0; Rebuild(); }
        private void Rebuild()
        {
            if (page != null) { page.SetActive(false); Destroy(page); }
            string[] titles = { "修炼", "背包", "仙途指引", "宗门", "活动", "福利", "更多", "道侣", "排行", "洞府", "设置" };
            var d = Q.Page(transform, "FeatureV4_" + current, titles[(int)current], Close, out page);
            switch (current)
            {
                case Feature.Cultivation: Cultivation(d); break;
                case Feature.Inventory: Inventory(d); break;
                case Feature.Task: Tasks(d); break;
                case Feature.Sect: Sect(d); break;
                case Feature.Activity: Activity(d); break;
                case Feature.Welfare: Welfare(d); break;
                case Feature.Partner: Partner(d); break;
                case Feature.Rank: Rank(d); break;
                case Feature.Estate: Estate(d); break;
                case Feature.Settings: Settings(d); break;
                default: More(d); break;
            }
            page.transform.SetAsLastSibling();
        }
        private void Tabs(Transform d, string[] names, float y = 175)
        {
            float w = 821f / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                Q.Tab(d, "Tab" + i, names[i], 60 + i * w, y, w, category == i, () => { category = index; Rebuild(); });
            }
            Q.Divider(d, 55, y + 76, 831);
        }
        private static void Caption(Transform d, string text, float y)
        {
            Q.Divider(d, 160, y + 23, 110); Q.Divider(d, 671, y + 23, 110);
            Q.Text(d, "ServiceState", text, 280, y, 381, 46, 25, Q.Muted);
        }
        private static void Illustration(Transform d, string key, float x, float y, float w, float h)
        {
            var art = Q.Art(d, key, x, y, w, h, true);
            if (art.sprite == null) art.color = Color.clear;
        }
        private static void SettingsIcon(Transform parent, string key, string fallback, float x, float y, float size)
        {
            Q.Icon(parent, Q.Sprite(key) != null ? key : fallback, x, y, size, size);
        }
        private static void ServiceCard(Transform d, string name, string title, string description,
            string art, float y, string actionLabel = "尚未开放", UnityAction action = null)
        {
            var card = CompactInfoCard.Create(d, name, 55, y, 831, 190);
            Illustration(card.transform, art, 22, 27, 136, 136);
            Q.Text(card.transform, "Name", title, 182, 23, 410, 48, 34, Q.Ink, TextAlignmentOptions.MidlineLeft);
            Q.Text(card.transform, "Description", description, 182, 80, 410, 85, 26, Q.Muted, TextAlignmentOptions.TopLeft);
            Q.Button(card.transform, "Action", actionLabel, 631, 60, 170, 78, action, action == null ? "button_disabled" : "button_secondary", 27);
        }
        private void Cultivation(Transform d)
        {
            BuildCultivation(d);
        }
        private void Inventory(Transform d)
        {
            Tabs(d, new[] { "全部", "装备", "材料", "消耗品" });
            Illustration(d, "inventory_empty", 205, 465, 531, 465);
            Caption(d, "背包数据待同步", 965);
            string[] help = {
                "当前背包服务尚未接入\n已同步的装备可在灵器仓库查看",
                "装备数据可在灵器仓库查看\n此页尚未接入背包列表",
                "材料列表与数量尚未接入\n暂无可显示的道具数据",
                "消耗品列表与数量尚未接入\n暂不可使用道具"
            };
            Q.Text(d, "InventoryHelp", help[Mathf.Clamp(category, 0, help.Length - 1)], 155, 1042, 631, 108, 28, Q.Muted);
            Q.Button(d, "Equipment", "查看灵器仓库", 230, 1215, 481, 96, OpenEquipment, "button_secondary");
        }
        private void Tasks(Transform d)
        {
            Tabs(d, new[] { "主线", "每日", "成就" });
            if (category == 0)
            {
                ServiceCard(d, "BattleGuide", "秘境探索", "游历山海，寻找秘境。\n在地图中查看各处入口。", "task_gate", 305, "前往", OpenRealm);
                ServiceCard(d, "EquipmentGuide", "灵器养成", "查看已有灵器，\n更换身上装备。", "task_furnace", 521, "前往", OpenEquipment);
                ServiceCard(d, "TalentGuide", "天赋修习", "参悟天赋，规划修习。\n天赋配置保存在本机。", "task_scripture", 737, "前往", OpenTalent);
                Caption(d, "进度与奖励待同步", 976);
            }
            else
            {
                Illustration(d, "task_scripture", 240, 440, 461, 400);
                Caption(d, category == 1 ? "每日任务尚未开放" : "成就数据尚未开放", 900);
                Q.Text(d, "TasksEmpty", "目标、完成进度与奖励尚未接入。\n可切换主线查看现有玩法入口。", 145, 1000, 651, 110, 28, Q.Muted);
                Q.Button(d, "OpenMainGuide", "查看仙途指引", 230, 1195, 481, 96, () => { category = 0; Rebuild(); }, "button_secondary");
            }
        }
        private void Sect(Transform d)
        {
            CompactInfoCard.Create(d, "SectIntroduction", 55, 220, 831, 190);
            Illustration(d, "task_gate", 78, 242, 146, 146);
            Q.Text(d, "SectState", "宗门数据待同步", 252, 243, 590, 48, 34, Q.Ink, TextAlignmentOptions.Left);
            Q.Text(d, "SectServiceState", "宗门列表、成员与公告尚未接入\n加入宗门与捐献暂不可用", 252, 304, 590, 80, 26, Q.Muted, TextAlignmentOptions.Left);
            string[] labels = { "宗门试炼", "藏经阁", "贡献商店", "成员名册" };
            string[] icons = { "sect", "talent", "welfare", "character" };
            for (int i = 0; i < labels.Length; i++) EntryCard(d, "SectEntry" + i, labels[i], "尚未开放", icons[i], 65 + i % 2 * 421, 460 + i / 2 * 174, null);
            Q.Button(d, "Donate", "宗门捐献 · 未开放", 230, 840, 481, 86, null, "button_disabled", 28);
        }
        private void Activity(Transform d)
        {
            Tabs(d, new[] { "限时", "日常", "全部" });
            Illustration(d, "activity", 340, 480, 261, 261);
            Caption(d, "暂无开放活动", 825);
            Q.Text(d, "ActivityEmpty", "活动日程、参与条件与奖励尚未接入。", 130, 925, 681, 100, 28, Q.Muted);
        }
        private void Welfare(Transform d)
        {
            Illustration(d, "welfare", 310, 355, 321, 321);
            Caption(d, "福利尚未开放", 755);
            Q.Text(d, "WelfareEmpty", "领取条件与奖励内容尚未接入\n暂无可领取的福利", 145, 850, 651, 110, 28, Q.Muted);
            Q.Button(d, "Claim", "暂无可领取内容", 230, 1080, 481, 96, null, "button_disabled");
        }
        private void More(Transform d)
        {
            string[] names = { "修炼", "背包", "任务", "宗门", "活动", "福利", "道侣", "排行", "洞府", "设置" };
            Feature[] targets = { Feature.Cultivation, Feature.Inventory, Feature.Task, Feature.Sect, Feature.Activity, Feature.Welfare, Feature.Partner, Feature.Rank, Feature.Estate, Feature.Settings };
            string[] icons = { "cultivation", "inventory", "quest", "sect", "activity", "welfare", "character", "rank", "sect", "settings" };
            string[] descriptions = { "灵根与天赋", "查看背包", "玩法指引", "尚未开放", "尚未开放", "尚未开放", "尚未开放", "尚未开放", "尚未开放", "声音与画面" };
            for (int i = 0; i < names.Length; i++)
            {
                Feature target = targets[i];
                EntryCard(d, "Destination" + i, names[i], descriptions[i], icons[i], 65 + i % 2 * 421, 220 + i / 2 * 176, () => ChangeFeature(target));
            }
        }
        private static void EntryCard(Transform d, string name, string title, string state, string icon, float x, float y, UnityAction action)
        {
            var button = Q.Button(d, name, "", x, y, 390, 148, action, null);
            var surface = CompactInfoCard.Create(button.transform, "Surface", 0, 0, 390, 148);
            surface.transform.SetAsFirstSibling();
            button.targetGraphic = surface;
            var colors = button.colors; colors.disabledColor = new Color(.91f, .92f, .90f, 1); button.colors = colors;
            Q.Icon(button.transform, icon, 24, 36, 76, 76);
            Q.Text(button.transform, "Name", title, 122, 24, 240, 44, 30, Q.Ink, TextAlignmentOptions.MidlineLeft);
            Q.Text(button.transform, "State", state, 122, 82, 240, 36, 24, Q.Muted, TextAlignmentOptions.MidlineLeft);
        }
        private void Partner(Transform d)
        {
            Illustration(d, "hero-character-female", 270, 190, 401, 650);
            Caption(d, "道侣服务尚未开放", 900);
            Q.Text(d, "PartnerEmpty", "羁绊与道侣数据尚未接入\n暂不可查看羁绊或安排上阵", 135, 990, 671, 110, 28, Q.Muted);
            Q.Button(d, "Bond", "羁绊 · 未开放", 85, 1265, 370, 96, null, "button_disabled", 28);
            Q.Button(d, "DeployPartner", "上阵 · 未开放", 485, 1265, 370, 96, null, "button_disabled", 28);
        }
        private void Rank(Transform d)
        {
            Tabs(d, new[] { "战力榜", "秘境榜", "宗门榜" });
            Q.Text(d, "RankColumn", "名次", 95, 290, 110, 50, 27, Q.Muted);
            Q.Text(d, "PlayerColumn", "道友", 260, 290, 330, 50, 27, Q.Muted, TextAlignmentOptions.MidlineLeft);
            Q.Text(d, "ScoreColumn", "数值", 710, 290, 130, 50, 27, Q.Muted);
            Q.Divider(d, 75, 350, 791);
            Q.Text(d, "RankEmpty", "暂无排名数据", 145, 430, 651, 55, 30, Q.Muted);
            CompactInfoCard.Create(d, "MyRankCard", 55, 580, 831, 108);
            Q.Text(d, "MyRank", "我的排名  —", 85, 607, 440, 54, 30, Q.Muted, TextAlignmentOptions.MidlineLeft);
            Q.Button(d, "RefreshRank", "刷新 · 未开放", 595, 594, 255, 80, null, "button_disabled", 26);
            Caption(d, "排行服务尚未开放", 740);
        }
        private void Estate(Transform d)
        {
            CompactInfoCard.Create(d, "EstateIntroduction", 55, 220, 831, 190);
            Illustration(d, "task_gate", 78, 242, 146, 146);
            Q.Text(d, "EstateState", "洞府尚未开放", 252, 243, 590, 48, 34, Q.Ink, TextAlignmentOptions.Left);
            Q.Text(d, "EstateEmpty", "洞府等级、建筑与产出尚未接入", 252, 304, 590, 75, 26, Q.Muted, TextAlignmentOptions.Left);
            string[] labels = { "灵田", "藏经阁", "炼丹房", "静修室" };
            string[] icons = { "jade_leaf", "talent", "flame", "cultivation" };
            for (int i = 0; i < labels.Length; i++) EntryCard(d, "EstateBuilding" + i, labels[i], "尚未开放", icons[i], 65 + i % 2 * 421, 460 + i / 2 * 174, null);
            Q.Button(d, "CollectEstate", "收取 · 未开放", 85, 840, 370, 86, null, "button_disabled", 28);
            Q.Button(d, "UpgradeEstate", "升级 · 未开放", 485, 840, 370, 86, null, "button_disabled", 28);
        }
        private void Settings(Transform d)
        {
            var sound = CompactInfoCard.Create(d, "VolumeCard", 55, 240, 831, 200);
            SettingsIcon(sound.transform, "audio_volume", "settings", 30, 26, 60);
            Q.Text(sound.transform, "VolumeTitle", "主音量", 120, 24, 360, 52, 34, Q.Ink, TextAlignmentOptions.MidlineLeft);
            float volume = Mathf.Clamp01(PlayerPrefs.GetFloat("UI.MasterVolume", 1f));
            var value = Q.Text(sound.transform, "VolumeValue", Mathf.RoundToInt(volume * 100) + "%", 646, 24, 140, 52, 28, Q.Muted);
            BuildVolumeSlider(sound.transform, volume, v =>
            {
                AudioListener.volume = v; PlayerPrefs.SetFloat("UI.MasterVolume", v); PlayerPrefs.Save();
                value.text = Mathf.RoundToInt(v * 100) + "%";
            });
            var mute = CompactInfoCard.Create(d, "MuteCard", 55, 465, 831, 130);
            SettingsIcon(mute.transform, "audio_mute", "settings", 30, 35, 60);
            Q.Text(mute.transform, "MuteTitle", "静音", 120, 36, 360, 55, 34, Q.Ink, TextAlignmentOptions.MidlineLeft);
            var muteButton = Q.Button(mute.transform, "Mute", "", 624, 26, 160, 77, null, null);
            muteButton.interactable = true;
            var toggle = Q.Rect(muteButton.transform, "Toggle", 10, 12, 140, 53).gameObject.AddComponent<QuietInkSettingShape>();
            toggle.raycastTarget = false;
            toggle.SetState(AudioListener.pause);
            muteButton.onClick.AddListener(() => { AudioListener.pause = !AudioListener.pause; toggle.SetState(AudioListener.pause); });
            string[] qualities = QualitySettings.names;
            int rows = Mathf.Max(1, Mathf.CeilToInt(qualities.Length / 3f));
            float qualityHeight = Mathf.Clamp(112 + rows * 76, 188, 340);
            var quality = CompactInfoCard.Create(d, "QualityCard", 55, 620, 831, qualityHeight);
            SettingsIcon(quality.transform, "quality_landscape", "sect", 30, 25, 60);
            Q.Text(quality.transform, "QualityTitle", "画面质量", 120, 25, 600, 52, 34, Q.Ink, TextAlignmentOptions.MidlineLeft);
            var qualityContent = Q.Scroll(quality.transform, "QualityChoices", 120, 96, 664, qualityHeight - 112, rows * 76);
            for (int i = 0; i < qualities.Length; i++)
            {
                int selectedQuality = i;
                Q.Button(qualityContent, "Quality" + i, QualityLabel(qualities[i]), i % 3 * 221, i / 3 * 76, 207, 68, () =>
                {
                    QualitySettings.SetQualityLevel(selectedQuality); PlayerPrefs.SetInt("UI.Quality", selectedQuality); PlayerPrefs.Save(); Rebuild();
                }, QualitySettings.GetQualityLevel() == i ? "button_primary" : "button_secondary", 26);
            }
            float helpY = 660 + qualityHeight;
            Q.Divider(d, 100, helpY, 741);
            SettingsIcon(d, "control_gamepad", "settings", 105, helpY + 46, 60);
            Q.Text(d, "ControlsHelp", "轻触进入页面 · 上下滑动浏览列表\n操作自定义与独立音乐、音效音量暂未开放", 192, helpY + 38, 644, 110, 25, Q.Muted, TextAlignmentOptions.MidlineLeft);
            Q.Text(d, "SaveHelp", "音量与画质调整后自动保存在本机", 115, helpY + 175, 711, 45, 25, Q.Muted);
            Q.Button(d, "ResetSettings", "恢复声音默认", 85, helpY + 250, 370, 86, () =>
            {
                PlayerPrefs.SetFloat("UI.MasterVolume", 1f); PlayerPrefs.Save(); AudioListener.volume = 1; AudioListener.pause = false; Rebuild();
            }, "button_secondary", 28);
            Q.Button(d, "CloseSettings", "完成", 485, helpY + 250, 370, 86, Close);
        }
        private static string QualityLabel(string value)
        {
            switch (value)
            {
                case "Very Low": return "极低";
                case "Low": return "低";
                case "Medium": return "中";
                case "High": return "高";
                case "Very High": return "极高";
                case "Ultra": return "超高";
                default: return value;
            }
        }
        private static void BuildVolumeSlider(Transform parent, float value, UnityAction<float> changed)
        {
            var r = Q.Rect(parent, "MasterVolume", 120, 95, 664, 80);
            r.gameObject.AddComponent<Image>().color = Color.clear;
            var slider = r.gameObject.AddComponent<Slider>();
            Q.Art(r, "health_track", 0, 31, 664, 18);
            var fillArea = Q.Rect(r, "FillArea", 17, 35, 630, 10);
            var fill = Q.Art(fillArea, "health_fill", 0, 0, 630, 10); fill.color = Q.Jade;
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var handleArea = Q.Rect(r, "HandleArea", 17, 15, 630, 50);
            var handle = Q.Rect(handleArea, "VolumeKnob", 0, 0, 44, 44).gameObject.AddComponent<QuietInkSettingShape>();
            handle.SetKnobOnly(); handle.raycastTarget = false;
            handle.rectTransform.pivot = new Vector2(.5f, .5f);
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(.5f, .5f);
            handle.rectTransform.sizeDelta = new Vector2(44, 44); handle.rectTransform.anchoredPosition = Vector2.zero;
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight; slider.minValue = 0; slider.maxValue = 1; slider.value = value;
            slider.onValueChanged.AddListener(changed);
        }
        private void OpenEquipment()
        {
            Close(); if (UIManager.Instance == null) return; UIManager.Instance.Show(UIScreen.Character);
            var c = FindObjectOfType<CharacterPanel>(true);
            if (c != null) (c.GetComponent<RoleEquipmentPanel>() ?? c.gameObject.AddComponent<RoleEquipmentPanel>()).Open();
        }
        private void OpenTalent()
        {
            Close(); if (UIManager.Instance == null) return; UIManager.Instance.Show(UIScreen.Character);
            var c = FindObjectOfType<CharacterPanel>(true);
            if (c != null) (c.GetComponent<RoleTalentPanel>() ?? c.gameObject.AddComponent<RoleTalentPanel>()).Open();
        }
        private void OpenRealm() { Close(); RealmMapScreen.Show(transform); }
    }

    /// <summary>Texture-free setting tracks and circular handles at their actual UI size.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class QuietInkSettingShape : MaskableGraphic
    {
        private bool isOn, knobOnly;
        public void SetState(bool value) { isOn = value; SetVerticesDirty(); }
        public void SetKnobOnly() { knobOnly = true; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            float radius = Mathf.Min(r.width, r.height) * .5f;
            if (radius <= 0) return;
            var center = r.center;
            if (knobOnly)
            {
                Circle(vh, center, radius, Q.Gold);
                Circle(vh, center, radius - 2.5f, Q.Paper);
                Circle(vh, center, radius - 5f, Q.Gold);
                return;
            }
            Color track = isOn ? Q.Jade : new Color(.59f, .61f, .58f, 1);
            float left = r.xMin + radius, right = r.xMax - radius;
            Circle(vh, new Vector2(left, center.y), radius, track);
            Circle(vh, new Vector2(right, center.y), radius, track);
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(left, r.yMin), track, Vector2.zero);
            vh.AddVert(new Vector3(left, r.yMax), track, Vector2.zero);
            vh.AddVert(new Vector3(right, r.yMax), track, Vector2.zero);
            vh.AddVert(new Vector3(right, r.yMin), track, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
            Circle(vh, new Vector2(isOn ? right : left, center.y), radius - 4f, Q.Paper);
        }
        private static void Circle(VertexHelper vh, Vector2 center, float radius, Color color)
        {
            const int segments = 40;
            int start = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                if (i > 0) vh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}

