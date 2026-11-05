using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using XiuXian.Network;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    public sealed partial class RoleSoulJadePanel : MonoBehaviour
    {
        private const string ArtRoot = "UI/SoulJade/DesignElements/";
        private const string SoulJadeIconAtlas = "UI/QuietInkV3/SoulJade/icons";
        private static readonly Dictionary<string, Sprite> SoulJadeIcons = new Dictionary<string, Sprite>();
        private static readonly Color Gold = Q.Gold;
        private static readonly Color Jade = Q.Jade;
        private static readonly Color Ivory = Q.Ink;
        private static readonly Color Muted = Q.Muted;

        private static readonly string[] JadeIconAliases =
        {
            "21_Lieyang", "22_Guanri", "23_Xuanwu", "24_Canglang", "25_Chixiao",
            "26_Fulong", "27_Hanxue", "28_Qinglian", "29_Zixiao", "30_Jinwu"
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
            public string InstanceId;
            public string Name;
            public string Rarity;
            public string IconKey;
            public int ConfigId;
            public int JadeIndex;
            public int Level = 1;
            public int MaxLevel;
            public long UpgradeCost;
            public long RefineCost;
            public List<JadeAffix> Affixes = new List<JadeAffix>();
        }

        private static readonly string[] AffixNames = { "生命", "攻击", "防御", "速度", "冰爆", "天雷" };
        private static string FormatAffix(JadeAffix affix) => affix.Key >= 4
            ? AffixNames[affix.Key]
            : AffixNames[affix.Key] + "  +" + affix.Value.ToString("0.#") + "%";
        private readonly List<OwnedJade> ownedJades = new List<OwnedJade>();
        private readonly int[] equipped = { -1, -1, -1, -1, -1, -1 };
        private readonly int[] visibleSlotIds = { -1, -1, -1, -1, -1, -1 };
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
        private Button equipButton;
        private Button removeButton;
        private Button syncButton;
        private TMP_Text serverStatusLabel;
        private SoulJadeService soulService;
        private bool serviceBound;

        private GameObject page;
        private GameObject toast;
        private TMP_Text detailName;
        private TMP_Text detailRarity;
        private TMP_Text detailEffect;
        private TMP_Text detailLore;
        private TMP_Text equipLabel;
        private int slotSelected;
        private int itemSelected = -1;
        private Coroutine toastRoutine;

        private void Awake()
        {
            Build();
        }

        public void Open()
        {
            if (page == null) Build();
            BindService();
            page.SetActive(true);
            page.transform.SetAsLastSibling();
            ApplyServerState();
            soulService.Reload();
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

        private void OnDisable()
        {
            UnbindService();
            Close();
        }

        private void RefreshSlots()
        {
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int itemIndex = equipped[i];
                Sprite sprite = itemIndex >= 0 && itemIndex < ownedJades.Count
                    ? OwnedIcon(ownedJades[itemIndex]) : null;
                SetJadeIcon(slotIcons[i], sprite);
                if (emptySlotLabels[i] != null) emptySlotLabels[i].gameObject.SetActive(sprite == null);
                if (emptySlotLabels[i] != null) emptySlotLabels[i].text = visibleSlotIds[i] >= 0 ? "＋" : "未开放";
                slotButtons[i].interactable = visibleSlotIds[i] >= 0;
                slotOutlines[i].enabled = i == slotSelected;
                slotOutlines[i].effectColor = i == slotSelected
                    ? new Color(Jade.r, Jade.g, Jade.b, 0.98f)
                    : new Color(Gold.r, Gold.g, Gold.b, 0.32f);
            }
        }

        private void RefreshInventory()
        {
            while (inventoryScroll != null && itemButtons.Count < ownedJades.Count) AddInventoryCard(itemButtons.Count);
            if (ownedCountLabel != null) ownedCountLabel.text = "已拥有 " + ownedJades.Count + " 枚";
            for (int i=0;i<itemButtons.Count;i++)
            {
                bool valid = i < ownedJades.Count;
                itemButtons[i].gameObject.SetActive(valid);
                if (!valid) continue;
                OwnedJade owned = ownedJades[i];
                SetJadeIcon(itemIcons[i], OwnedIcon(owned));
                itemSelectionOutlines[i].enabled = i == itemSelected;
                itemSelectionOutlines[i].effectColor = i == itemSelected
                    ? new Color(Jade.r, Jade.g, Jade.b, .9f) : new Color(Gold.r, Gold.g, Gold.b, 0f);
            }
        }

        private void RefreshDetail()
        {
            if (detailName == null) return;
            bool ready = soulService != null && soulService.CanMutate;
            if (serverStatusLabel != null) serverStatusLabel.text = soulService != null ? soulService.Status : "登录后同步魂玉";
            if (syncButton != null) syncButton.interactable = soulService != null && !soulService.IsBusy;
            if (removeButton != null) removeButton.interactable = ready && visibleSlotIds[slotSelected] >= 0 && equipped[slotSelected] >= 0;
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
                if (equipButton != null) equipButton.interactable = false;
                return;
            }

            OwnedJade owned = ownedJades[index];
            detailName.text = owned.Name;
            detailRarity.text = owned.Rarity + "  ·  Lv." + owned.Level + "  ·  词条 " + owned.Affixes.Count;
            detailRarity.color = Rarity(owned.Rarity);
            var lines = new List<string>();
            for (int i=0;i<owned.Affixes.Count;i++)
            {
                JadeAffix affix = owned.Affixes[i];
                lines.Add(FormatAffix(affix));
            }
            detailEffect.text = string.Join("\n", lines);
            equipLabel.text = equipped[slotSelected] == index ? "已镶嵌" : "镶嵌";
            if (equipButton != null) equipButton.interactable = ready && visibleSlotIds[slotSelected] >= 0 && equipped[slotSelected] != index;
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
            RefreshSlots();
            RefreshInventory();
            RefreshDetail();
        }

        private void EquipSelected()
        {
            OwnedJade selected = GetSelectedOwned();
            if (selected == null) { Toast("请先选择一枚魂玉"); return; }
            BindService();
            if (!soulService.Equip(visibleSlotIds[slotSelected], selected.InstanceId)) Toast(soulService.Status);
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

        private void RemoveSelected()
        {
            if (equipped[slotSelected] < 0) return;
            BindService();
            if (!soulService.Equip(visibleSlotIds[slotSelected], string.Empty)) Toast(soulService.Status);
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

        private static Color Rarity(string rarity)
        {
            return rarity == "神品" ? Q.Gold : rarity == "极品" ? Q.Blue : Q.Jade;
        }

        private static Sprite LoadIcon(string name)
        {
            int jadeIndex = Array.IndexOf(JadeIconAliases, name);
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










