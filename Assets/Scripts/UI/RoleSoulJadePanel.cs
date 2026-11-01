using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    public sealed partial class RoleSoulJadePanel : MonoBehaviour
    {
        private const string SaveKey = "RoleSoulJadeLoadoutV1";
        private const string ArtRoot = "UI/SoulJade/DesignElements/";
        private const string SoulJadeIconAtlas = "UI/QuietInkV3/SoulJade/icons";
        private static readonly Dictionary<string, Sprite> SoulJadeIcons = new Dictionary<string, Sprite>();
        private static readonly Color Gold = Q.Gold;
        private static readonly Color Jade = Q.Jade;
        private static readonly Color Ivory = Q.Ink;
        private static readonly Color Muted = Q.Muted;
        private static readonly string[] SlotNames = { "近战", "远程", "防护", "生存", "技能", "奥义" };

        private sealed class JadeDef
        {
            public readonly string Name;
            public readonly string Type;
            public readonly string Rarity;
            public readonly string Effect;
            public readonly string Lore;
            public readonly string Icon;

            public JadeDef(string name, string type, string rarity, string effect, string lore, string icon)
            {
                Name = name;
                Type = type;
                Rarity = rarity;
                Effect = effect;
                Lore = lore;
                Icon = icon;
            }
        }

        private static readonly JadeDef[] Jades =
        {
            new JadeDef("烈阳·破阵", "近战", "神品", "近战伤害 +8%", "灼热灵息贯穿锋刃，近身交锋时更易撕开防线。", "21_Lieyang"),
            new JadeDef("贯日·长弓", "远程", "神品", "远程伤害 +10%", "以灵力校准弓弦，令远距离压制更加稳定。", "22_Guanri"),
            new JadeDef("玄武·磐石", "防护", "极品", "护甲上限 +12%", "厚重灵息护住周身，在混战中稳住阵脚。", "23_Xuanwu"),
            new JadeDef("苍狼·疾步", "生存", "极品", "闪避距离 +9%", "身轻如风，闪避后短时间内提升移动速度。", "24_Canglang"),
            new JadeDef("赤霄·无妄", "技能", "神品", "技能伤害 +9%", "引动赤色灵焰，让每次技能释放都更具威势。", "25_Chixiao"),
            new JadeDef("伏龙·裂空", "奥义", "极品", "奥义伤害 +11%", "沉睡龙息破空而出，强化关键时刻的终结能力。", "26_Fulong"),
            new JadeDef("寒雪·凝霜", "防护", "极品", "受击减伤 +8%", "凝霜成甲，抵御来袭锋芒。", "27_Hanxue"),
            new JadeDef("青莲·回生", "生存", "极品", "治疗效果 +12%", "青莲灵息温养经脉，恢复生机。", "28_Qinglian"),
            new JadeDef("紫霄·惊雷", "技能", "神品", "技能冷却 -6%", "紫电贯脉，灵力运转更为迅捷。", "29_Zixiao"),
            new JadeDef("金乌·焚天", "奥义", "神品", "奥义充能 +10%", "金乌真火汇聚，积蓄焚天之势。", "30_Jinwu"),
        };

        [Serializable]
        private sealed class JadeAffix
        {
            public int Key;
            public float Value;
        }

        [Serializable]
        private sealed class OwnedJade
        {
            public int JadeIndex;
            public int Level = 1;
            public List<JadeAffix> Affixes = new List<JadeAffix>();
        }

        [Serializable]
        private sealed class InventorySave
        {
            public List<OwnedJade> Items = new List<OwnedJade>();
        }

        private const string InventorySaveKey = "RoleSoulJadeInventoryV1";
        private static readonly string[] AffixNames = { "近战伤害", "远程伤害", "技能伤害", "奥义伤害", "闪避距离", "护甲上限", "技能冷却", "治疗效果", "怒气获取", "伤害减免", "体力恢复", "暴击伤害" };
        private readonly List<OwnedJade> ownedJades = new List<OwnedJade>();
        private readonly int[] equipped = { 0, 1, 2, 3, 4, 5 };
        private readonly Button[] slotButtons = new Button[6];
        private readonly Image[] slotIcons = new Image[6];
        private readonly Outline[] slotOutlines = new Outline[6];
        private readonly List<Button> itemButtons = new List<Button>();
        private readonly List<Image> itemIcons = new List<Image>();
        private readonly List<Outline> itemSelectionOutlines = new List<Outline>();
        private ScrollRect inventoryScroll;
        private GridLayoutGroup inventoryGrid;
        private TMP_Text ownedCountLabel;
        private TMP_Text upgradeLabel;
        private TMP_Text refineLabel;
        private Button upgradeButton;
        private Button refineButton;

        private GameObject page;
        private GameObject toast;
        private TMP_Text detailName;
        private TMP_Text detailRarity;
        private TMP_Text detailEffect;
        private TMP_Text detailLore;
        private TMP_Text equipLabel;
        private int slotSelected;
        private int itemSelected;
        private Coroutine toastRoutine;

        private void Awake()
        {
            LoadInventory();
            Load();
            Build();
        }

        public void Open()
        {
            if (page == null) Build();
            page.SetActive(true);
            page.transform.SetAsLastSibling();
            RefreshSlots();
            RefreshInventory();
            RefreshDetail();
        }

        public void Close()
        {
            if (operationView != null) operationView.SetActive(false);
            if (page != null) page.SetActive(false);
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = null;
            if (toast != null) { toast.SetActive(false); Destroy(toast); }
            toast = null;
        }

        private void OnDisable() { Close(); }

        private void RefreshSlots()
        {
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int itemIndex = equipped[i];
                Sprite sprite = itemIndex >= 0 && itemIndex < ownedJades.Count
                    ? LoadIcon(Jades[ownedJades[itemIndex].JadeIndex].Icon) : null;
                SetJadeIcon(slotIcons[i], sprite);
                if (emptySlotLabels[i] != null) emptySlotLabels[i].gameObject.SetActive(sprite == null);
                slotOutlines[i].enabled = i == slotSelected;
                slotOutlines[i].effectColor = i == slotSelected
                    ? new Color(Jade.r, Jade.g, Jade.b, 0.98f)
                    : new Color(Gold.r, Gold.g, Gold.b, 0.32f);
            }
        }

        private void RefreshInventory()
        {
            if (ownedCountLabel != null) ownedCountLabel.text = "已拥有 " + ownedJades.Count + " 枚";
            for (int i=0;i<itemButtons.Count;i++)
            {
                bool valid = i < ownedJades.Count;
                itemButtons[i].gameObject.SetActive(valid);
                if (!valid) continue;
                OwnedJade owned = ownedJades[i];
                JadeDef definition = Jades[owned.JadeIndex];
                SetJadeIcon(itemIcons[i], LoadIcon(definition.Icon));
                itemSelectionOutlines[i].enabled = i == itemSelected;
                itemSelectionOutlines[i].effectColor = i == itemSelected
                    ? new Color(Jade.r, Jade.g, Jade.b, .9f) : new Color(Gold.r, Gold.g, Gold.b, 0f);
            }
        }

        private void RefreshDetail()
        {
            if (detailName == null) return;
            int index = itemSelected >= 0 ? itemSelected : equipped[slotSelected];
            if (index < 0 || index >= ownedJades.Count)
            {
                detailName.text = "请选择一枚魂玉";
                detailRarity.text = "";
                detailEffect.text = "";
                equipLabel.text = "镶嵌";
                upgradeLabel.text = "升级";
                refineLabel.text = "洗练";
                upgradeButton.interactable = refineButton.interactable = false;
                return;
            }

            OwnedJade owned = ownedJades[index];
            JadeDef item = Jades[owned.JadeIndex];
            detailName.text = item.Name;
            detailRarity.text = item.Rarity + "  ·  Lv." + owned.Level + "  ·  词条 " + owned.Affixes.Count + "/4";
            detailRarity.color = Rarity(item.Rarity);
            var lines = new List<string>();
            for (int i=0;i<owned.Affixes.Count;i++)
            {
                JadeAffix affix = owned.Affixes[i];
                lines.Add(AffixNames[affix.Key] + "  +" + affix.Value.ToString("0.#") + "%");
            }
            detailEffect.text = string.Join("\n", lines);
            equipLabel.text = "装备";
            upgradeLabel.text = "升级";
            refineLabel.text = "洗练";
            upgradeButton.interactable = true;
            refineButton.interactable = true;
        }

        private void SelectSlot(int index)
        {
            slotSelected = index;
            itemSelected = equipped[index];
            RefreshInventory();
            RefreshSlots();
            RefreshDetail();
        }

        private void SelectLibraryJade(int index)
        {
            if (index < 0 || index >= ownedJades.Count) return;
            itemSelected = index;
            slotSelected = Array.IndexOf(SlotNames, Jades[ownedJades[index].JadeIndex].Type);
            RefreshSlots();
            RefreshInventory();
            RefreshDetail();
        }

        private void EquipSelected()
        {
            if(itemSelected<0 || itemSelected>=ownedJades.Count){Toast("请先选择一枚魂玉");return;}
            JadeDef item=Jades[ownedJades[itemSelected].JadeIndex];
            if(item.Type!=SlotNames[slotSelected]){Toast("该魂玉与当前槽位不匹配");return;}
            equipped[slotSelected]=itemSelected;
            Save(); RefreshSlots(); RefreshDetail(); Toast("已装备「"+item.Name+"」");
        }

        private void ShowUpgradeView()
        {
            if (GetSelectedOwned() == null) { Toast("请先选择一枚魂玉"); return; }
            BuildOperationView(true);
        }

        private void ShowRefineView()
        {
            if (GetSelectedOwned() == null) { Toast("请先选择一枚魂玉"); return; }
            BuildOperationView(false);
        }
        private OwnedJade GetSelectedOwned()
        {
            int index=itemSelected>=0?itemSelected:equipped[slotSelected];
            return index>=0 && index<ownedJades.Count ? ownedJades[index] : null;
        }

        public void AddOwnedJade(int jadeIndex)
        {
            if (jadeIndex < 0 || jadeIndex >= Jades.Length) return;
            ownedJades.Add(CreateOwnedJade(jadeIndex));
            if (inventoryScroll != null) AddInventoryCard(ownedJades.Count - 1);
            SaveInventory();
            RefreshInventory();
        }

        private static OwnedJade CreateOwnedJade(int jadeIndex)
        {
            var item = new OwnedJade { JadeIndex = jadeIndex, Level = 1, Affixes = new List<JadeAffix>() };
            var keys = new List<int>();
            for (int key = 0; key < AffixNames.Length; key++) keys.Add(key);
            int count = UnityEngine.Random.Range(2, 4);
            for (int i = 0; i < count; i++)
            {
                int pick = UnityEngine.Random.Range(0, keys.Count);
                int key = keys[pick];
                keys.RemoveAt(pick);
                item.Affixes.Add(new JadeAffix { Key = key, Value = UnityEngine.Random.Range(4, 10) });
            }
            return item;
        }
        private void LoadInventory()
        {
            ownedJades.Clear();
            string json = PlayerPrefs.GetString(InventorySaveKey, "");
            bool hasSavedInventory = false;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    InventorySave saved = JsonUtility.FromJson<InventorySave>(json);
                    if (saved != null)
                    {
                        hasSavedInventory = true;
                        if (saved.Items != null)
                        {
                            foreach (OwnedJade item in saved.Items)
                            {
                                if (item == null || item.JadeIndex < 0 || item.JadeIndex >= Jades.Length) continue;
                                if (item.Affixes == null) item.Affixes = new List<JadeAffix>();
                                item.Level = Mathf.Max(item.Level, 1);
                                item.Affixes.RemoveAll(a => a == null || a.Key < 0 || a.Key >= AffixNames.Length);
                                if (item.Affixes.Count > 4) item.Affixes.RemoveRange(4, item.Affixes.Count - 4);
                                ownedJades.Add(item);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    ownedJades.Clear();
                    hasSavedInventory = false;
                }
            }

            if (!hasSavedInventory)
            {
                // Local demo inventory until the player-inventory service is connected.
                for (int i = 0; i < Jades.Length; i++) ownedJades.Add(CreateOwnedJade(i));
                SaveInventory();
            }
        }
        private void SaveInventory()
        {
            InventorySave saved=new InventorySave();
            saved.Items=ownedJades;
            PlayerPrefs.SetString(InventorySaveKey,JsonUtility.ToJson(saved));
            PlayerPrefs.Save();
        }

        private void RemoveSelected()
        {
            equipped[slotSelected] = -1;
            itemSelected = -1;
            Save();
            RefreshSlots();
            RefreshInventory();
            RefreshDetail();
            Toast("已卸下「" + SlotNames[slotSelected] + "」魂玉");
        }

        private void Toast(string message)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            if (toast != null) Destroy(toast);
            var panel = Panel(page.transform, "SoulJadeToast",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -540f),
                new Vector2(600f, 64f), new Vector2(0.5f, 0.5f),
                Q.Paper);
            toast = panel.gameObject;
            AddOutline(toast, new Color(Jade.r, Jade.g, Jade.b, 0.72f));
            Text(toast.transform, "Message", message, 19,
                Vector2.zero, new Vector2(570f, 56f), new Vector2(0.5f, 0.5f),
                Ivory, TextAlignmentOptions.Center, true);
            toastRoutine = StartCoroutine(HideToast());
        }

        private IEnumerator HideToast()
        {
            yield return new WaitForSeconds(1.7f);
            if (toast != null) Destroy(toast);
            toast = null;
            toastRoutine = null;
        }

        private void Load()
        {
            string data = PlayerPrefs.GetString(SaveKey, "");
            if (string.IsNullOrEmpty(data)) return;
            string[] values = data.Split(',');
            if (values.Length != equipped.Length) return;
            for (int i = 0; i < values.Length; i++)
            {
                if (!int.TryParse(values[i], out int value) || value < -1 || value >= ownedJades.Count)
                {
                    equipped[i] = ownedJades.FindIndex(j => Jades[j.JadeIndex].Type == SlotNames[i]);
                    continue;
                }
                if (value >= 0 && Jades[ownedJades[value].JadeIndex].Type != SlotNames[i])
                    value = ownedJades.FindIndex(j => Jades[j.JadeIndex].Type == SlotNames[i]);
                equipped[i] = value;
            }
        }
        private void Save()
        {
            PlayerPrefs.SetString(SaveKey,string.Join(",",equipped));
            PlayerPrefs.Save();
        }

        private static Color Rarity(string rarity)
        {
            return rarity == "神品" ? Q.Gold : rarity == "极品" ? Q.Blue : Q.Jade;
        }

        private static Sprite LoadIcon(string name)
        {
            int jadeIndex = Array.FindIndex(Jades, item => item.Icon == name);
            if (jadeIndex >= 0)
            {
                string spriteName = "soul_jade_" + jadeIndex.ToString("00");
                if (SoulJadeIcons.TryGetValue(spriteName, out var cached) && cached != null) return cached;
                foreach (var sprite in Resources.LoadAll<Sprite>(SoulJadeIconAtlas))
                    SoulJadeIcons[sprite.name] = sprite;
                if (SoulJadeIcons.TryGetValue(spriteName, out var updated) && updated != null) return updated;
            }

            string icon;
            switch (name)
            {
                case "21_Lieyang": case "30_Jinwu": icon = "jade_flame"; break;
                case "22_Guanri": case "28_Qinglian": icon = "jade_leaf"; break;
                case "23_Xuanwu": case "27_Hanxue": icon = "jade_disk"; break;
                case "24_Canglang": icon = "jade_cloud"; break;
                case "25_Chixiao": case "29_Zixiao": icon = "jade_spiral"; break;
                case "26_Fulong": icon = "jade_comma"; break;
                default: return Resources.Load<Sprite>(ArtRoot + name);
            }
            return Q.Sprite(icon);
        }

        private static void SetJadeIcon(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.preserveAspect = true;
            image.enabled = sprite != null;
            image.color = sprite != null ? Color.white : Color.clear;
        }

        private static Image Art(Transform parent, string name, string asset,
            Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivotOverride = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivotOverride ?? (anchor.x > 0.5f && anchor.y >= 1f ? new Vector2(0.5f, 1f) : anchor);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = LoadIcon(asset);
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static Image StretchArt(Transform parent, string name, string asset, Color color)
        {
            var viewport = new GameObject(name + "AspectViewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(parent, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect);
            viewport.transform.SetAsFirstSibling();

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(viewport.transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Sprite sprite = LoadIcon(asset);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;
            image.maskable = true;

            if (sprite != null)
            {
                var fitter = go.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            }
            return image;
        }
        private static Button SpriteButton(Transform parent, string name, string asset,
            Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot, UnityAction action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.sprite = LoadIcon(asset);
            image.preserveAspect = true;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(action);
            return button;
        }

        private static Button HitTarget(Transform parent, string name,
            Vector2 anchor, Vector2 position, Vector2 size, UnityAction action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.001f);
            image.raycastTarget = true;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (action != null) button.onClick.AddListener(action);
            return button;
        }

        private static Button ColorButton(Transform parent, string name,
            Vector2 anchor, Vector2 position, Vector2 size, Color color, UnityAction action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (action != null) button.onClick.AddListener(action);
            return button;
        }

        private static RectTransform Panel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static RectTransform Panel(Transform parent, string name,
            Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static TMP_Text Text(Transform parent, string name, string value, int size,
            Vector2 position, Vector2 box, Vector2 anchor, Color color,
            TextAlignmentOptions alignment, bool bold, Vector2? pivotOverride = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivotOverride ?? anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static void AddOutline(GameObject target, Color color)
        {
            var outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}










