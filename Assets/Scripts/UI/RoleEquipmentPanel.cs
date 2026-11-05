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
        private readonly int[] _displayedSlotIds = { -1, -1, -1, -1, -1, -1 };

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
        private Button _changeButton;
        private Button _resyncButton;
        private readonly Button[] _slotButtons = new Button[6];
        private readonly TMP_Text[] _slotLabels = new TMP_Text[6];
        private readonly Image[] _slotIcons = new Image[6];
        private readonly Image[] _slotFrames = new Image[6];
        private readonly List<Button> _itemButtons = new();
        private RoleEquipmentData _data;
        private int _selectedSlot = 1;
        private bool _requestPending;
        private bool _isSynchronized;
        private bool _pendingIsChange;
        private ulong _pendingRequestId;
        private float _requestDeadline;
        private static ulong _nextRequestId = (ulong)DateTime.UtcNow.Ticks;
        private const float RequestTimeoutSeconds = 12f;

        /// <summary>登录会话切换时清除装备快照，不创建新的网络或页面对象。</summary>
        public static void ClearSession()
        {
            foreach (var panel in FindObjectsByType<RoleEquipmentPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                panel.ResetSession();
        }

        private void ResetSession()
        {
            _data = null;
            _isSynchronized = false;
            _requestPending = false;
            _pendingRequestId = 0;
            if (_picker != null) _picker.SetActive(false);
            ShowUnavailableView();
            RefreshArtDetail();
            SetControlsInteractable(false);
        }

        private void OnDisconnected(string reason)
        {
            ResetSession();
            ShowSyncFailure("连接已断开，请连接服务器后重新同步");
        }

        private void Update()
        {
            if (!_requestPending || Time.unscaledTime < _requestDeadline) return;
            _requestPending = false;
            _pendingRequestId = 0;
            ShowSyncFailure("同步超时，请重新同步确认装备状态");
        }

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
            net.OnDisconnected += OnDisconnected;
        }

        private void OnDisable()
        {
            Close();
            _requestPending = false;
            _pendingRequestId = 0;
            _isSynchronized = false;
            if (!Application.isPlaying)
                return;

            var net = NetworkManager.Instance;
            if (net == null)
                return;
            net.UnregisterHandler("GetRoleEquipmentAck");
            net.UnregisterHandler("ChangeRoleEquipmentAck");
            net.OnDisconnected -= OnDisconnected;
        }

        public void OpenSlot(int slot)
        {
            _selectedSlot = slot;
            Open();
        }

        public void Open()
        {
            if (_page == null)
                BuildPage();
            var character = GetComponent<CharacterPanel>();
            if (character != null) character.HideAttributes();
            _page.SetActive(true);
            _page.transform.SetAsLastSibling();
            if (_data == null) ShowUnavailableView();
            else RefreshView();
            SetStatus("正在同步角色装备…", Ice);
            RequestLoadout();
            RefreshArtDetail();
        }

        private void Close()
        {
            bool wasOpen = _page != null && _page.activeSelf;
            if (_picker != null) _picker.SetActive(false);
            if (_page != null)
                _page.SetActive(false);
            if (wasOpen && isActiveAndEnabled)
            {
                var character = GetComponent<CharacterPanel>();
                if (character != null) character.ShowAttributes();
            }
        }

        private void RequestLoadout()
        {
            if (_requestPending) return;
            var net = NetworkManager.Instance;
            if (net == null || !net.IsConnected)
            {
                ShowSyncFailure("连接服务器后可查看装备");
                return;
            }

            BeginRequest(false);
            SetStatus("正在同步角色装备…", Ice);
            net.SendProto(new GetRoleEquipmentReq { RequestId = _pendingRequestId });
        }

        private void BeginRequest(bool isChange)
        {
            _requestPending = true;
            _pendingIsChange = isChange;
            _isSynchronized = false;
            _pendingRequestId = ++_nextRequestId;
            _requestDeadline = Time.unscaledTime + RequestTimeoutSeconds;
            SetControlsInteractable(false);
            if (_resyncButton != null) _resyncButton.gameObject.SetActive(false);
        }

        private bool CompleteRequest(ulong requestId, bool isChange)
        {
            if (!_requestPending || _pendingRequestId != requestId || _pendingIsChange != isChange) return false;
            _requestPending = false;
            _pendingRequestId = 0;
            return true;
        }

        private void ShowSyncFailure(string message)
        {
            _isSynchronized = false;
            SetControlsInteractable(false);
            SetStatus(message, Q.Cinnabar);
            if (_resyncButton != null) _resyncButton.gameObject.SetActive(true);
        }

        private void ApplySnapshot(RoleEquipmentData data, string status)
        {
            _data = data;
            _isSynchronized = true;
            RefreshView();
            SetControlsInteractable(true);
            if (_resyncButton != null) _resyncButton.gameObject.SetActive(false);
            SetStatus(status, Q.Jade);
        }

        private void ShowUnavailableView()
        {
            RefreshSlotDirectory();
            if (_attributeText != null)
                _attributeText.text = "装备加成 · 等待服务器同步";
            for (int i = 0; i < _slotLabels.Length; i++)
            {
                if (_slotLabels[i] != null)
                    _slotLabels[i].text = "槽位 " + (i + 1);
                if (_slotButtons[i] != null)
                    _slotButtons[i].interactable = false;
                RefreshSlotArt(i, false);
            }
            if (_unequipButton != null) _unequipButton.interactable = false;
            if (_inventoryTitle != null)
                _inventoryTitle.text = "灵器仓库 · 等待同步";
            if (_emptyInventoryText != null)
            {
                _emptyInventoryText.text = "登录后同步灵器仓库\n当前槽位的装备会显示在这里";
                _emptyInventoryText.gameObject.SetActive(true);
            }
            ResizePicker(0);
        }


        private void OnLoadoutReceived(GetRoleEquipmentAck ack)
        {
            if (!CompleteRequest(ack.RequestId, false)) return;
            if (!ack.HasCode || ack.Code != ErrCode.Ok || ack.Data == null)
            {
                ShowSyncFailure(!ack.HasCode || ack.Code != ErrCode.Ok ? LoadoutErrors.ResponseMessage(ack.HasCode, ack.Code) : "装备数据暂不可用");
                return;
            }

            ApplySnapshot(ack.Data, "装备已与道庭同步");
        }

        private void OnChangeReceived(ChangeRoleEquipmentAck ack)
        {
            if (!CompleteRequest(ack.RequestId, true)) return;
            if (!ack.HasCode || ack.Code != ErrCode.Ok || ack.Data == null)
            {
                ShowSyncFailure(!ack.HasCode || ack.Code != ErrCode.Ok ? LoadoutErrors.ResponseMessage(ack.HasCode, ack.Code) : "装备操作失败");
                return;
            }

            ApplySnapshot(ack.Data, "装备已更新 · 后续战斗使用新属性");
            CombatStatsService.Invalidate();
        }

private void SelectSlot(int slot)
        {
            if (_data != null && FindSlotDefinition(slot) == null) return;
            _selectedSlot = slot;
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
            if (_requestPending || !_isSynchronized || _data == null)
                return;
            var net = NetworkManager.Instance;
            if (net == null || !net.IsConnected)
            {
                ShowSyncFailure("连接服务器后可更换装备");
                return;
            }

            BeginRequest(true);
            SetStatus("正在更新装备…", Ice);
            net.SendProto(new ChangeRoleEquipmentReq
            {
                Slot = (EquipSlot)_selectedSlot,
                EquipmentId = instanceId ?? string.Empty,
                RequestId = _pendingRequestId,
            });
        }

        private void RefreshView()
        {
            if (_data == null)
                return;
            RefreshSlotDirectory();
            if (FindSlotDefinition(_selectedSlot) == null)
                _selectedSlot = _displayedSlotIds.Length > 0 ? _displayedSlotIds[0] : -1;

            if (_attributeText != null)
            {
                _attributeText.text = $"装备加成 · 生命 +{_data.MaxHp:N0}   攻击 +{_data.Attack:N0}   防御 +{_data.Defense:N0}   速度 +{_data.Speed:N0}";
            }

            for (int index = 0; index < _displayedSlotIds.Length; index++)
            {
                int slot = _displayedSlotIds[index];
                var equipped = FindEquipped(slot);
                _slotLabels[index].text = SlotName(slot) + (equipped == null ? "" : " Lv." + equipped.Level);

                RefreshSlotArt(index, equipped != null);
            }

            if (_inventoryTitle != null)
                _inventoryTitle.text = $"{SlotName(_selectedSlot)} · 可用灵器";
            if (_unequipButton != null)
                _unequipButton.interactable = _isSynchronized && !_requestPending && FindEquipped(_selectedSlot) != null;
            RebuildItemList();
            RefreshArtDetail();
        }

        private void RefreshSlotArt(int index, bool equipped)
        {
            int slot = _displayedSlotIds[index];
            bool selected = slot >= 0 && slot == _selectedSlot;
            if (_slotFrames[index] != null)
                _slotFrames[index].sprite = Q.Sprite(selected ? "equipment_slot_selected" : "equipment_slot");
            if (_slotIcons[index] != null)
            {
                _slotIcons[index].sprite = EquipmentIcon(FindEquipped(slot), FindSlotDefinition(slot));
                _slotIcons[index].enabled = _slotIcons[index].sprite != null;
                _slotIcons[index].color = equipped ? Color.white : new Color(1, 1, 1, .78f);
            }
            if (_slotLabels[index] != null)
                _slotLabels[index].color = selected ? Q.Jade : Q.Ink;
        }

        private void RefreshSlotDirectory()
        {
            for (int i = 0; i < _displayedSlotIds.Length; i++) _displayedSlotIds[i] = -1;
            if (_data == null) return;
            var slots = new List<EquipmentSlotDefinition>(_data.Slots);
            slots.Sort((a, b) => ((int)a.Slot).CompareTo((int)b.Slot));
            for (int i = 0; i < slots.Count && i < _displayedSlotIds.Length; i++) _displayedSlotIds[i] = (int)slots[i].Slot;
        }

        private EquipmentSlotDefinition FindSlotDefinition(int slot)
        {
            if (_data != null) foreach (var definition in _data.Slots) if ((int)definition.Slot == slot) return definition;
            return null;
        }

        private string SlotName(int slot)
        {
            var definition = FindSlotDefinition(slot);
            if (definition != null && !string.IsNullOrEmpty(definition.Name)) return definition.Name;
            return slot > 0 ? "槽位 " + slot : "未开放";
        }

        private static Sprite EquipmentIcon(EquipmentItem item, EquipmentSlotDefinition slot)
        {
            if (item != null && !string.IsNullOrEmpty(item.IconKey))
            {
                var icon = Q.Sprite(item.IconKey);
                if (icon != null) return icon;
            }
            return slot == null || string.IsNullOrEmpty(slot.IconKey) ? null : Q.Sprite(slot.IconKey);
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
            button.interactable = _isSynchronized && !_requestPending && !equipped;
            return button;
        }

        private string DisplayName(EquipmentItem item)
        {
            string name = string.IsNullOrEmpty(item.DisplayName) ? "未命名装备" : item.DisplayName;
            return string.IsNullOrEmpty(item.Rarity) ? name : item.Rarity + "·" + name;
        }

        private static string AttributeLine(EquipmentItem item) =>
            $"生命 +{item.MaxHp:N0}   攻击 +{item.Attack:N0}   防御 +{item.Defense:N0}   速度 +{item.Speed:N0}";

        private void SetControlsInteractable(bool interactable)
        {
            interactable = interactable && _isSynchronized && !_requestPending;
            if (_changeButton != null) _changeButton.interactable = interactable && FindSlotDefinition(_selectedSlot) != null;
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                if (_slotButtons[i] != null)
                    _slotButtons[i].interactable = interactable && FindSlotDefinition(_displayedSlotIds[i]) != null;
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
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                int slot = i + 1;
                int visualIndex = i;
                float x = 74 + (i % 3) * 288, y = 212 + (i / 3) * 258;
                var button = Q.Button(d, "Slot_" + slot, "", x, y, 216, 226, () => SelectSlot(_displayedSlotIds[visualIndex]), "equipment_slot");
                _slotButtons[i] = button;
                _slotFrames[i] = button.GetComponent<Image>();
                _slotIcons[i] = Q.Icon(button.transform, "equipment", 37, 18, 142, 142);
                _slotIcons[i].enabled = false;
                _slotLabels[i] = Q.Text(button.transform, "SlotLabel" + slot, "槽位 " + slot, 9, 174, 198, 38, 27);
                _slotLabels[i].enableWordWrapping = false;
                _slotLabels[i].enableAutoSizing = true;
                _slotLabels[i].fontSizeMin = 23;
                _slotLabels[i].fontSizeMax = 28;
            }
            Q.Text(d, "LoadoutHeading", "当前穿戴", 65, 153, 600, 40, 28, Q.Ink, TextAlignmentOptions.Left);
            _attributeText = Q.Text(d, "Attributes", "装备加成 · 等待服务器同步", 65, 744, 811, 46, 24, Q.Muted);
            _attributeText.enableWordWrapping = false;
            _attributeText.enableAutoSizing = true;
            _attributeText.fontSizeMin = 20;
            _attributeText.fontSizeMax = 24;

            CompactInfoCard.Create(d, "EquipmentDetails", 50, 840, 841, 352);
            _detailIcon = Q.Icon(d, "equipment", 82, 866, 96, 96);
            _detailIcon.enabled = false;
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
            _changeButton = Q.Button(d, "Change", "穿戴 / 更换", 80, 1230, 376, 80,
                () => { _picker.SetActive(true); _picker.transform.SetAsLastSibling(); });
            _unequipButton = Q.Button(d, "Unequip", "卸下", 485, 1230, 376, 80, () => ChangeEquipment(string.Empty), "button_secondary");
            _statusText = Q.Text(d, "Status", "", 75, 1332, 791, 36, 23, Q.Muted);
            _resyncButton = Q.Button(d, "Resync", "重新同步", 300, 1396, 341, 78,
                RequestLoadout, "button_secondary");
            _resyncButton.gameObject.SetActive(false);

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
            var item = FindEquipped(_selectedSlot);
            _detailName.text = item == null ? SlotName(_selectedSlot) : DisplayName(item);
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
            _detailIcon.sprite = EquipmentIcon(item, FindSlotDefinition(_selectedSlot));
            _detailIcon.enabled = _detailIcon.sprite != null;
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
