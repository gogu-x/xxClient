using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XiuXian.Network;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    public class CharacterPanel : MonoBehaviour
    {
        public const string SelectedCharacterKey = "SelectedLobbyCharacter";
        public const string PageRootName = "CharacterSelectV4";
        [SerializeField] private RawImage previewImage;
        [SerializeField] private CharacterPreviewStage previewStage;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text placeholderInfoText;
        private GameObject page, selectionDialog;
        private Image hero;
        private TMP_Text roleName, roleTitle, confirmText;
        private TMP_Text attributeState;
        private Button refreshAttributesButton;
        private readonly TMP_Text[] attributeValues = new TMP_Text[4];
        private readonly TMP_Text[] choices = new TMP_Text[2];
        private readonly Image[] choiceFrames = new Image[2];
        private int selected;

        private void Awake() { BuildPage(); }
        private void OnEnable()
        {
            CombatStatsService.Changed -= RefreshAttributeDisplay;
            CombatStatsService.Invalidated -= OnAttributesInvalidated;
            CombatStatsService.Changed += RefreshAttributeDisplay;
            CombatStatsService.Invalidated += OnAttributesInvalidated;
            if (previewStage != null) previewStage.SetActiveStage(false);
            if (previewImage != null) previewImage.gameObject.SetActive(false);
            if (placeholderInfoText != null) placeholderInfoText.gameObject.SetActive(false);
            EnsurePageReferences();
            page.SetActive(true);
            selectionDialog.SetActive(false);
            Select(Mathf.Clamp(PlayerPrefs.GetInt(SelectedCharacterKey, 0), 0, 1));
            ShowAttributes();
        }
        private void OnDisable()
        {
            CombatStatsService.Changed -= RefreshAttributeDisplay;
            CombatStatsService.Invalidated -= OnAttributesInvalidated;
            if (previewStage != null) previewStage.SetActiveStage(false);
        }

        public void HideAttributes() { if (page != null) page.SetActive(false); }

        public void ShowAttributes()
        {
            if (!isActiveAndEnabled) return;
            EnsurePageReferences();
            page.SetActive(true);
            RefreshAttributeDisplay();
            RefreshAttributes();
        }

        private void RefreshAttributes()
        {
            if (Application.isPlaying && isActiveAndEnabled && page != null && page.activeInHierarchy)
                CombatStatsService.RequestRefresh();
        }

        private void OnAttributesInvalidated() { RefreshAttributes(); }

        private bool HasPageReferences()
        {
            if (page == null || hero == null || selectionDialog == null || roleName == null ||
                roleTitle == null || confirmText == null || attributeState == null || refreshAttributesButton == null)
                return false;
            foreach (var value in attributeValues) if (value == null) return false;
            for (int i = 0; i < choices.Length; i++)
                if (choices[i] == null || choiceFrames[i] == null) return false;
            return true;
        }

        private void EnsurePageReferences()
        {
            if (HasPageReferences()) return;
            // Hot reload can retain page/hero while resetting readonly arrays of UI references.
            bool wasVisible = page != null && page.activeSelf;
            BuildPage();
            page.SetActive(wasVisible);
        }

        private void RefreshAttributeDisplay()
        {
            var stats = CombatStatsService.Data;
            long[] values = stats == null ? null : new[] { stats.MaxHp, stats.Attack, stats.Defense, stats.Speed };
            for (int i = 0; i < attributeValues.Length; i++)
            {
                if (attributeValues[i] == null) continue;
                attributeValues[i].text = values == null ? "—" : values[i].ToString("N0");
                attributeValues[i].color = values == null ? Q.Muted : Q.Ink;
            }
            if (attributeState != null) attributeState.text = CombatStatsService.Status;
            if (refreshAttributesButton != null) refreshAttributesButton.interactable = !CombatStatsService.IsPending;
        }

        private void BuildPage()
        {
            foreach (string name in new[] { PageRootName, "CharacterSelectV2", "CharacterSelectV3", "SkinWardrobeV1", "TitleText", "InfoCard" })
            {
                var child = transform.Find(name);
                if (child != null) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            }
            var d = Q.Page(transform, PageRootName, "角色", Back, out page);
            Q.Tab(d, "CharacterTab", "属性", 100, 140, 247, true, () => { });
            Q.Tab(d, "EquipmentTab", "装备", 347, 140, 247, false, OpenEquipment);
            Q.Tab(d, "TalentTab", "天赋", 594, 140, 247, false, OpenTalents);
            Q.Divider(d, 55, 215, 831);

            Q.Art(d, "character-scene", 0, 185, 941, 855).color = new Color(1, 1, 1, .68f);
            hero = Q.Art(d, "hero-character", 165, 245, 611, 735, true);
            string[] equipment = { "sword", "crown", "robe", "jade_pendant", "bracers", "boots" };
            int[] equipmentSlots = { 1, 2, 3, 5, 4, 6 };
            for (int i = 0; i < equipment.Length; i++)
            {
                int slotId = equipmentSlots[i];
                float x = i % 2 == 0 ? 55 : 726;
                float y = 280 + (i / 2) * 218;
                var slot = Q.Button(d, "EquipmentShortcut" + i, "", x, y, 160, 160,
                    () => (GetComponent<RoleEquipmentPanel>() ?? gameObject.AddComponent<RoleEquipmentPanel>()).OpenSlot(slotId), "equipment_slot");
                Q.Icon(slot.transform, equipment[i], 25, 22, 110, 116);
            }
            roleName = Q.Text(d, "RoleName", "", 220, 966, 501, 49, 34);
            roleTitle = Q.Text(d, "RoleTitle", "", 190, 1014, 561, 34, 23, Q.Muted);
            Q.Button(d, "ChangeAppearance", "切换形象", 716, 968, 170, 55,
                () => selectionDialog.SetActive(true), null, 24);

            CompactInfoCard.Create(d, "AttributeCard", 55, 1070, 831, 258);
            Q.Text(d, "AttributesTitle", "角色属性", 85, 1090, 250, 44, 32, Q.Ink, TextAlignmentOptions.Left);
            Q.Divider(d, 85, 1144, 771);
            string[] attributes = { "气血", "攻击", "防御", "速度" };
            string[] icons = { "health", "attack", "defense", "speed" };
            for (int i = 0; i < attributes.Length; i++)
            {
                float x = 90 + (i % 2) * 402;
                float y = 1162 + (i / 2) * 72;
                Q.Icon(d, icons[i], x, y + 5, 32, 32);
                Q.Text(d, "AttributeName" + i, attributes[i], x + 49, y, 160, 44, 28, Q.Ink, TextAlignmentOptions.Left);
                attributeValues[i] = Q.Text(d, "AttributeValue" + i, "—", x + 222, y, 140, 44, 29, Q.Muted, TextAlignmentOptions.Right);
            }
            attributeState = Q.Text(d, "AttributeState", "角色属性待同步", 335, 1093, 400, 38, 21, Q.Muted, TextAlignmentOptions.Right);
            refreshAttributesButton = Q.Button(d, "RefreshAttributes", "刷新", 750, 1090, 105, 44,
                RefreshAttributes, "button_secondary", 23);
            Q.Button(d, "ViewEquipment", "查看装备", 80, 1370, 376, 83, OpenEquipment);
            Q.Button(d, "ViewTalents", "天赋", 485, 1370, 376, 83, OpenTalents, "button_secondary");
            BuildSelectionDialog();
        }

        private void BuildSelectionDialog()
        {
            var r = Q.Rect(page.transform, "AppearanceSelection", 0, 0, 0, 0);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            selectionDialog = r.gameObject;
            r.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.18f, 0.16f, .56f);
            var d = Q.Design(r);
            CompactInfoCard.Create(d, "AppearancePanel", 70, 485, 801, 660);
            Q.Text(d, "AppearanceTitle", "选择展示形象", 170, 515, 601, 58, 38);
            Q.Text(d, "AppearanceScope", "仅保存本机展示形象", 170, 582, 601, 38, 25, Q.Muted);
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                float x = 130 + 365 * i;
                var card = Q.Button(d, "Choice" + i, "", x, 640, 315, 292, () => Select(index), "equipment_slot");
                choiceFrames[i] = card.GetComponent<Image>();
                Q.Icon(card.transform, i == 0 ? "hero-character" : "hero-character-female", 55, 17, 205, 195);
                Q.Text(card.transform, "Name", i == 0 ? "清风道人" : "云梦仙子", 15, 213, 285, 40, 28);
                choices[i] = Q.Text(card.transform, "ChoiceState", "", 15, 254, 285, 32, 23, Q.Jade);
            }
            var confirm = Q.Button(d, "ConfirmAppearance", "选定角色", 130, 1005, 315, 84, Confirm);
            confirmText = confirm.GetComponentInChildren<TMP_Text>();
            Q.Button(d, "CancelAppearance", "取消", 495, 1005, 315, 84, () =>
            {
                Select(Mathf.Clamp(PlayerPrefs.GetInt(SelectedCharacterKey, 0), 0, 1));
                selectionDialog.SetActive(false);
            }, "button_secondary");
            selectionDialog.SetActive(false);
        }

        private void Select(int index)
        {
            EnsurePageReferences();
            selected = index;
            hero.sprite = Q.Sprite(index == 0 ? "hero-character" : "hero-character-female");
            hero.color = hero.sprite != null ? Color.white : Color.clear;
            roleName.text = index == 0 ? "清风道人" : "云梦仙子";
            roleTitle.text = hero.sprite == null ? "形象资源待载入" : (index == 0 ? "剑修 · 御剑乘风" : "灵修 · 月华灵韵");
            int saved = Mathf.Clamp(PlayerPrefs.GetInt(SelectedCharacterKey, 0), 0, 1);
            for (int i = 0; i < choices.Length; i++)
            {
                choices[i].text = i == index ? (i == saved ? "当前形象" : "预览") : "选择";
                choiceFrames[i].sprite = Q.Sprite(i == index ? "equipment_slot_selected" : "equipment_slot");
            }
            confirmText.text = "选定角色";
        }
        private void Confirm()
        {
            PlayerPrefs.SetInt(SelectedCharacterKey, selected); PlayerPrefs.Save();
            selectionDialog.SetActive(false); Select(selected);
        }
        private void Back() { UIManager.Instance.Show(UIScreen.Main); }
        private void OpenEquipment() { (GetComponent<RoleEquipmentPanel>() ?? gameObject.AddComponent<RoleEquipmentPanel>()).Open(); }
        private void OpenTalents() { (GetComponent<RoleTalentPanel>() ?? gameObject.AddComponent<RoleTalentPanel>()).Open(); }

    }
}
