using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XiuXian.Network;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    /// <summary>Server-authoritative equipment view hosted inside the character screen.</summary>
    public sealed class RoleEquipmentPanel : MonoBehaviour
    {
        private const string PageName = "RoleEquipmentV4";
        private GameObject _picker;
        private RectTransform _pickerSurface;
        private RectTransform _pickerViewport;
        private Button _closePickerButton;
        private readonly TMP_Text[] _detailStats = new TMP_Text[4];
        private TMP_Text _detailName;
        private TMP_Text _detailLevel;
        private Image _detailIcon;
        private static readonly string[] SlotNames = { "灵剑", "仙冠", "法袍", "护腕", "玉佩", "云履" };
        private static readonly string[][] ItemNames =
        {
            new[] { "青锋剑", "落霞灵剑" },
            new[] { "云纹冠", "玄玉道冠" },
            new[] { "流云法袍", "玄霜仙衣" },
            new[] { "缚灵护腕", "星纹腕甲" },
            new[] { "温玉佩", "太虚灵佩" },
            new[] { "踏云靴", "追风云履" },
        };

        private static readonly Color Gold = Q.Gold;
        private static readonly Color Ice = Q.Blue;
        private static readonly Color Panel = new(0.018f, 0.065f, 0.09f, 0.96f);

        private TMP_FontAsset _font;
        private GameObject _page;
        private TMP_Text _attributeText;
        private TMP_Text _inventoryTitle;
        private TMP_Text _emptyInventoryText;
        private TMP_Text _statusText;
        private RectTransform _inventoryContent;
        private Button _unequipButton;
        private readonly Button[] _slotButtons = new Button[6];
        private readonly TMP_Text[] _slotLabels = new TMP_Text[6];
        private readonly Image[] _slotIcons = new Image[6];
        private readonly Image[] _slotFrames = new Image[6];
        private readonly List<Button> _itemButtons = new();
        private RoleEquipmentData _data;
        private int _selectedSlot = 1;
        private bool _requestPending;

        private void Awake()
        {
            _font = TMP_Settings.defaultFontAsset;
            BuildPage();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            var net = NetworkManager.Instance;
            if (net == null)
                return;
            net.RegisterProtoHandler("GetRoleEquipmentAck", GetRoleEquipmentAck.Parser, OnLoadoutReceived);
            net.RegisterProtoHandler("ChangeRoleEquipmentAck", ChangeRoleEquipmentAck.Parser, OnChangeReceived);
        }

        private void OnDisable()
        {
            Close();
            _requestPending = false;
            if (!Application.isPlaying)
                return;

            var net = NetworkManager.Instance;
            if (net == null)
                return;
            net.UnregisterHandler("GetRoleEquipmentAck");
            net.UnregisterHandler("ChangeRoleEquipmentAck");
        }

        public void OpenSlot(int slot)
        {
            _selectedSlot = Mathf.Clamp(slot, 1, SlotNames.Length);
            Open();
        }

public void Open()
        {
            if (_page == null)
                BuildPage();
            _page.SetActive(true);
            _page.transform.SetAsLastSibling();
            _selectedSlot = Mathf.Clamp(_selectedSlot, 1, 6);
            if (_data == null) ShowUnavailableView();
            else RefreshView();
            SetStatus("正在同步角色装备…", Ice);
            RequestLoadout();
            RefreshArtDetail();
        }

        private void Close()
        {
            if (_picker != null) _picker.SetActive(false);
            if (_page != null)
                _page.SetActive(false);
        }

        private void RequestLoadout()
        {
            var net = NetworkManager.Instance;
            if (net == null || !net.IsConnected)
            {
                SetStatus("连接服务器后可查看装备", Q.Cinnabar);
                return;
            }

            _requestPending = true;
            SetControlsInteractable(false);
            net.SendProto(new GetRoleEquipmentReq());
        }

private void ShowUnavailableView()
        {
            if (_attributeText != null)
                _attributeText.text = "角色属性 · 等待服务器同步";
            for (int i = 0; i < _slotLabels.Length; i++)
            {
                if (_slotLabels[i] != null)
                    _slotLabels[i].text = SlotNames[i];
                if (_slotButtons[i] != null)
                    _slotButtons[i].interactable = true;
                RefreshSlotArt(i, false);
            }
            if (_unequipButton != null) _unequipButton.interactable = false;
            if (_inventoryTitle != null)
                _inventoryTitle.text = SlotNames[_selectedSlot - 1] + " · 灵器仓库";
            if (_emptyInventoryText != null)
            {
                _emptyInventoryText.text = "登录后同步灵器仓库\n当前槽位的装备会显示在这里";
                _emptyInventoryText.gameObject.SetActive(true);
            }
            ResizePicker(0);
        }


        private void OnLoadoutReceived(GetRoleEquipmentAck ack)
        {
            _requestPending = false;
            SetControlsInteractable(true);
            if (!string.IsNullOrEmpty(ack.Error) || ack.Data == null)
            {
                SetStatus(string.IsNullOrEmpty(ack.Error) ? "装备数据暂不可用" : ack.Error,
                    Q.Cinnabar);
                return;
            }

            _data = ack.Data;
            RefreshView();
            SetStatus("装备已与道庭同步", Q.Jade);
        }

        private void OnChangeReceived(ChangeRoleEquipmentAck ack)
        {
            _requestPending = false;
            SetControlsInteractable(true);
            if (!string.IsNullOrEmpty(ack.Error) || ack.Data == null)
            {
                SetStatus(string.IsNullOrEmpty(ack.Error) ? "装备操作失败" : ack.Error,
                    Q.Cinnabar);
                return;
            }

            _data = ack.Data;
            RefreshView();
            SetStatus("穿戴完成 · 战斗属性已更新", Q.Jade);
        }

private void SelectSlot(int slot)
        {
            _selectedSlot = Mathf.Clamp(slot, 1, SlotNames.Length);
            RefreshArtDetail();
            if (_data == null)
            {
                ShowUnavailableView();
                return;
            }
            RefreshView();
        }

        private void ChangeEquipment(string instanceId)
        {
            if (_requestPending || _data == null)
                return;
            var net = NetworkManager.Instance;
            if (net == null || !net.IsConnected)
            {
                SetStatus("连接服务器后可更换装备", Q.Cinnabar);
                return;
            }

            _requestPending = true;
            SetControlsInteractable(false);
            net.SendProto(new ChangeRoleEquipmentReq
            {
                Slot = (EquipSlot)_selectedSlot,
                EquipmentId = instanceId ?? string.Empty,
            });
        }

        private void RefreshView()
        {
            if (_data == null)
                return;

            if (_attributeText != null)
            {
                _attributeText.text = $"生命 +{_data.MaxHp:N0}   攻击 +{_data.Attack:N0}   防御 +{_data.Defense:N0}   速度 +{_data.Speed:N0}";
            }

            for (int slot = 1; slot <= 6; slot++)
            {
                var equipped = FindEquipped(slot);
                _slotLabels[slot - 1].text = SlotNames[slot - 1] + (equipped == null ? "" : " Lv." + equipped.Level);

                RefreshSlotArt(slot - 1, equipped != null);
            }

            if (_inventoryTitle != null)
                _inventoryTitle.text = $"{SlotNames[_selectedSlot - 1]} · 可用灵器";
            if (_unequipButton != null)
                _unequipButton.interactable = !_requestPending && FindEquipped(_selectedSlot) != null;
            RebuildItemList();
            RefreshArtDetail();
        }

        private void RefreshSlotArt(int index, bool equipped)
        {
            bool selected = index + 1 == _selectedSlot;
            if (_slotFrames[index] != null)
                _slotFrames[index].sprite = Q.Sprite(selected ? "equipment_slot_selected" : "equipment_slot");
            if (_slotIcons[index] != null)
                _slotIcons[index].color = equipped ? Color.white : new Color(1, 1, 1, .78f);
            if (_slotLabels[index] != null)
                _slotLabels[index].color = selected ? Q.Jade : Q.Ink;
        }

        private EquipmentItem FindEquipped(int slot)
        {
            if (_data == null)
                return null;
            foreach (var item in _data.Items)
            {
                if ((int)item.Slot == slot && item.Equipped)
                    return item;
            }
            return null;
        }

private void RebuildItemList()
        {
            foreach (var button in _itemButtons)
            {
                if (button != null)
                {
                    if (Application.isPlaying) Destroy(button.gameObject);
                    else DestroyImmediate(button.gameObject);
                }
            }
            _itemButtons.Clear();

            int count = 0;
            foreach (var item in _data.Items)
            {
                if ((int)item.Slot != _selectedSlot)
                    continue;
                count++;
                var button = CreateItemButton(item);
                _itemButtons.Add(button);
            }
            if (_emptyInventoryText != null)
            {
                _emptyInventoryText.text = count == 0 ? "此槽位暂时没有灵器" : string.Empty;
                _emptyInventoryText.gameObject.SetActive(count == 0);
            }
            ResizePicker(count);
        }

        private Button CreateItemButton(EquipmentItem item)
        {
            bool equipped = item.Equipped;
            string value = $"{DisplayName(item)} · Lv.{item.Level}\n"
                + $"生命 +{item.MaxHp:N0}    攻击 +{item.Attack:N0}\n"
                + $"防御 +{item.Defense:N0}    速度 +{item.Speed:N0}\n"
                + (equipped ? "正在穿戴" : "点击穿戴");
            var button = Q.Button(_inventoryContent, $"Gear_{item.InstanceId}", value, 0, 0, 745, 158,
                () => ChangeEquipment(item.InstanceId), null, 25);
            button.GetComponent<Image>().color = equipped
                ? new Color32(226, 238, 226, 242) : new Color32(242, 239, 228, 235);
            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 158;
            layout.flexibleWidth = 1;
            var label = button.GetComponentInChildren<TMP_Text>();
            label.alignment = TextAlignmentOptions.Left;
            label.enableAutoSizing = false;
            label.enableWordWrapping = true;
            label.lineSpacing = 3;
            label.color = equipped ? Q.Jade : Q.Ink;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(24, 10);
            label.rectTransform.offsetMax = new Vector2(-24, -10);
            button.interactable = !_requestPending && !equipped;
            return button;
        }

        private string DisplayName(EquipmentItem item)
        {
            int slot = Mathf.Clamp((int)item.Slot - 1, 0, SlotNames.Length - 1);
            int variant = item.ConfigId > 6 ? 1 : 0;
            string rarity = item.ConfigId > 6 ? "玄品" : "凡品";
            return $"{rarity}·{ItemNames[slot][variant]}";
        }

        private static string AttributeLine(EquipmentItem item) =>
            $"生命 +{item.MaxHp:N0}   攻击 +{item.Attack:N0}   防御 +{item.Defense:N0}   速度 +{item.Speed:N0}";

        private void SetControlsInteractable(bool interactable)
        {
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                if (_slotButtons[i] != null)
                    _slotButtons[i].interactable = interactable;
            }
            foreach (var button in _itemButtons)
            {
                if (button != null)
                    button.interactable = interactable && !button.GetComponentInChildren<TMP_Text>().text.Contains("正在穿戴");
            }
            if (_unequipButton != null)
                _unequipButton.interactable = interactable && _data != null && FindEquipped(_selectedSlot) != null;
        }

        private void SetStatus(string value, Color color)
        {
            if (_statusText == null)
                return;
            _statusText.text = value;
            _statusText.color = color;
        }

private void BuildPage()
        {
            var d = Q.Page(transform, PageName, "灵器", Close, out _page);
            string[] icons = { "sword", "crown", "robe", "bracers", "jade_pendant", "boots" };
            for (int i = 0; i < icons.Length; i++)
            {
                int slot = i + 1;
                float x = 74 + (i % 3) * 288, y = 212 + (i / 3) * 258;
                var button = Q.Button(d, "Slot_" + slot, "", x, y, 216, 226, () => SelectSlot(slot), "equipment_slot");
                _slotButtons[i] = button;
                _slotFrames[i] = button.GetComponent<Image>();
                _slotIcons[i] = Q.Icon(button.transform, icons[i], 37, 18, 142, 142);
                _slotLabels[i] = Q.Text(button.transform, "SlotLabel" + slot, SlotNames[i], 9, 174, 198, 38, 27);
                _slotLabels[i].enableWordWrapping = false;
                _slotLabels[i].enableAutoSizing = true;
                _slotLabels[i].fontSizeMin = 23;
                _slotLabels[i].fontSizeMax = 28;
            }
            Q.Text(d, "LoadoutHeading", "当前穿戴", 65, 153, 600, 40, 28, Q.Ink, TextAlignmentOptions.Left);
            _attributeText = Q.Text(d, "Attributes", "角色属性 · 等待服务器同步", 65, 744, 811, 46, 24, Q.Muted);
            _attributeText.enableWordWrapping = false;
            _attributeText.enableAutoSizing = true;
            _attributeText.fontSizeMin = 20;
            _attributeText.fontSizeMax = 24;

            CompactInfoCard.Create(d, "EquipmentDetails", 50, 840, 841, 352);
            _detailIcon = Q.Icon(d, "sword", 82, 866, 96, 96);
            _detailName = Q.Text(d, "SelectedName", "", 204, 871, 630, 46, 32, Q.Ink, TextAlignmentOptions.Left);
            _detailName.enableWordWrapping = false;
            _detailName.enableAutoSizing = true;
            _detailName.fontSizeMin = 27;
            _detailLevel = Q.Text(d, "SelectedLevel", "", 204, 924, 630, 34, 24, Q.Muted, TextAlignmentOptions.Left);
            Q.Divider(d, 82, 985, 777);
            string[] stats = { "health", "attack", "defense", "speed" };
            string[] statNames = { "生命", "攻击", "防御", "速度" };
            for (int i = 0; i < stats.Length; i++)
            {
                float x = 84 + (i % 2) * 396, y = 1020 + (i / 2) * 74;
                Q.Icon(d, stats[i], x, y + 5, 28, 28);
                Q.Text(d, "StatName" + i, statNames[i], x + 42, y, 84, 40, 25, Q.Muted, TextAlignmentOptions.Left);
                _detailStats[i] = Q.Text(d, "StatValue" + i, "—", x + 140, y, 210, 40, 28, Q.Ink, TextAlignmentOptions.Right);
                _detailStats[i].enableWordWrapping = false;
                _detailStats[i].enableAutoSizing = true;
                _detailStats[i].fontSizeMin = 21;
                _detailStats[i].fontSizeMax = 28;
            }
            Q.Button(d, "Change", "穿戴 / 更换", 80, 1230, 376, 80,
                () => { _picker.SetActive(true); _picker.transform.SetAsLastSibling(); });
            _unequipButton = Q.Button(d, "Unequip", "卸下", 485, 1230, 376, 80, () => ChangeEquipment(string.Empty), "button_secondary");
            _statusText = Q.Text(d, "Status", "", 75, 1332, 791, 36, 23, Q.Muted);

            var overlay = Q.Rect(d, "EquipmentPickerOverlay", 0, 0, 941, 1672);
            overlay.gameObject.AddComponent<Image>().color = new Color(.12f, .18f, .16f, .58f);
            _picker = overlay.gameObject;
            var picker = CompactInfoCard.Create(overlay, "EquipmentPicker", 45, 210, 851, 1232).rectTransform;
            _pickerSurface = picker;
            _inventoryTitle = Q.Text(picker, "InventoryTitle", "灵器仓库", 45, 48, 761, 64, 36);
            Q.Text(picker, "ServerInventoryHint", "穿戴与属性以服务器数据为准", 45, 112, 761, 42, 25, Q.Muted);
            BuildInventoryScroll();
            var viewport = (RectTransform)_inventoryContent.parent;
            _pickerViewport = viewport;
            viewport.SetParent(picker, false);
            viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0, 1);
            viewport.anchoredPosition = new Vector2(32, -175);
            viewport.sizeDelta = new Vector2(787, 885);
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, .001f);
            _emptyInventoryText.color = Q.Muted;
            _closePickerButton = Q.Button(picker, "ClosePicker", "关闭", 225, 1100, 401, 78, () => _picker.SetActive(false), "button_secondary");
            ResizePicker(0);
            _picker.SetActive(false);
            _page.SetActive(false);
        }

        private void ResizePicker(int itemCount)
        {
            if (_pickerSurface == null || _pickerViewport == null || _closePickerButton == null) return;
            float listHeight = Mathf.Clamp(itemCount * 170f + 4, 220, 780);
            float height = 175 + listHeight + 130;
            _pickerSurface.sizeDelta = new Vector2(851, height);
            _pickerSurface.anchoredPosition = new Vector2(45, -(1672 - height) * .5f);
            _pickerViewport.sizeDelta = new Vector2(787, listHeight);
            ((RectTransform)_closePickerButton.transform).anchoredPosition = new Vector2(225, -(198 + listHeight));
        }

        private void RefreshArtDetail()
        {
            if (_detailName == null) return;
            string[] icons = { "sword", "crown", "robe", "bracers", "jade_pendant", "boots" };
            var item = FindEquipped(_selectedSlot);
            _detailName.text = item == null ? SlotNames[_selectedSlot - 1] : DisplayName(item);
            _detailLevel.text = item == null ? (_data == null ? "待同步" : "当前槽位未穿戴")
                : "Lv." + item.Level + " · 当前穿戴";
            if (item == null)
            {
                foreach (var value in _detailStats) if (value != null) value.text = "—";
            }
            else
            {
                _detailStats[0].text = $"+{item.MaxHp:N0}";
                _detailStats[1].text = $"+{item.Attack:N0}";
                _detailStats[2].text = $"+{item.Defense:N0}";
                _detailStats[3].text = $"+{item.Speed:N0}";
            }
            _detailIcon.sprite = Q.Sprite(icons[_selectedSlot - 1]);
        }

        private static string SlotGlyph(int slot)
        {
            switch (slot)
            {
                case 0: return "剑";
                case 1: return "冠";
                case 2: return "袍";
                case 3: return "腕";
                case 4: return "玉";
                default: return "履";
            }
        }


private void BuildInventoryScroll()
        {
            var viewport = new GameObject("InventoryViewport", typeof(RectTransform), typeof(Image),
                typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(_page.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0.525f, 0.15f);
            viewportRect.anchorMax = new Vector2(0.96f, 0.75f);
            viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(0.01f, 0.04f, 0.055f, 0.74f);

            var content = new GameObject("InventoryContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            _inventoryContent = content.GetComponent<RectTransform>();
            _inventoryContent.anchorMin = new Vector2(0f, 1f);
            _inventoryContent.anchorMax = new Vector2(1f, 1f);
            _inventoryContent.pivot = new Vector2(0.5f, 1f);
            _inventoryContent.anchoredPosition = Vector2.zero;
            _inventoryContent.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var empty = MakeText(content.transform, "EmptyInventory", "连接服务器后同步灵器仓库", 24,
                new Color(0.78f, 0.88f, 0.88f, 0.92f), TextAlignmentOptions.Center);
            empty.rectTransform.sizeDelta = new Vector2(0f, 160f);
            _emptyInventoryText = empty;
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 160f;

            var scrollRect = viewport.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = _inventoryContent;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 48f;

        }


private GameObject CreateCard(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var card = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
            card.transform.SetParent(parent, false);
            var rect = card.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = card.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>("UI/SoulJade/DesignElements/12_Item_Detail_Plaque");
            image.color = image.sprite != null ? Color.white : color;
            image.preserveAspect = false;
            image.raycastTarget = false;
            var outline = card.GetComponent<Outline>();
            outline.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.55f);
            outline.effectDistance = new Vector2(2f, -2f);
            return card;
        }

private Button CreateButton(Transform parent, string name, string label, Vector2 min, Vector2 max,
            Color color, UnityEngine.Events.UnityAction action, TextAlignmentOptions alignment, int fontSize)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            button.transform.SetParent(parent, false);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = button.GetComponent<Image>();
            image.color = color;
            if (name.StartsWith("Slot_"))
            {
                Sprite frame = Resources.Load<Sprite>("UI/SoulJade/DesignElements/12_Item_Detail_Plaque");
                if (frame != null)
                {
                    image.sprite = frame;
                    image.color = Color.white;
                    image.preserveAspect = false;
                }
            }
            var control = button.GetComponent<Button>();
            control.targetGraphic = image;
            control.transition = Selectable.Transition.None;
            if (action != null)
                control.onClick.AddListener(action);
            var outline = button.GetComponent<Outline>();
            outline.effectColor = new Color(0.46f, 0.68f, 0.73f, 0.5f);
            outline.effectDistance = new Vector2(2f, -2f);
            var text = MakeText(button.transform, "Label", label, fontSize,
                new Color(0.91f, 0.95f, 0.95f, 1f), alignment);
            Stretch(text.rectTransform, new Vector2(12f, 6f), new Vector2(-12f, -6f));
            return control;
        }

        private Button CreateButton(Transform parent, string name, string label, Color color,
            UnityEngine.Events.UnityAction action)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            button.transform.SetParent(parent, false);
            var image = button.GetComponent<Image>();
            image.color = color;
            var control = button.GetComponent<Button>();
            control.targetGraphic = image;
            control.transition = Selectable.Transition.None;
            if (action != null)
                control.onClick.AddListener(action);
            var outline = button.GetComponent<Outline>();
            outline.effectColor = new Color(0.34f, 0.65f, 0.72f, 0.48f);
            outline.effectDistance = new Vector2(2f, -2f);
            var text = MakeText(button.transform, "Label", label, 23,
                new Color(0.91f, 0.95f, 0.95f, 1f), TextAlignmentOptions.Center);
            Stretch(text.rectTransform, new Vector2(18f, 10f), new Vector2(-18f, -10f));
            return control;
        }

        private TMP_Text MakeText(Transform parent, string name, string value, int size,
            Color color, TextAlignmentOptions alignment, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.extraPadding = true;
            return text;
        }

        private TMP_Text MakeText(Transform parent, string name, string value, int size,
            Vector2 position, Vector2 dimensions, Vector2 anchor, Color color,
            TextAlignmentOptions alignment, bool bold = false)
        {
            var text = MakeText(parent, name, value, size, color, alignment, bold);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            return text;
        }

        private static void Stretch(RectTransform rect, Vector2? insetMin = null, Vector2? insetMax = null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = insetMin ?? Vector2.zero;
            rect.offsetMax = insetMax ?? Vector2.zero;
        }
    }
}
