using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    public sealed partial class RoleSoulJadePanel
    {
        private readonly TMP_Text[] emptySlotLabels = new TMP_Text[6];

        private void BuildQuietInkSoulJadePage()
        {
            design = Q.Page(transform, "SoulJadeArtDirectionPage", "魂玉", Close, out page);
            Q.Text(design, "LocalState", "本地配装 · 未接入服务器", 175, 126, 591, 36, 24, Q.Muted);
            Q.Tab(design, "SocketTab", "镶嵌", 100, 170, 247, true, CloseOperationView);
            refineButton = Q.Tab(design, "RefineTab", "洗练", 347, 170, 247, false, ShowRefineView);
            refineLabel = refineButton.GetComponentInChildren<TMP_Text>();
            upgradeButton = Q.Tab(design, "UpgradeTab", "升级", 594, 170, 247, false, ShowUpgradeView);
            upgradeLabel = upgradeButton.GetComponentInChildren<TMP_Text>();
            Q.Divider(design, 55, 245, 831);

            var ring = Q.Art(design, "soul_ring", 224, 275, 492, 492, true);
            ring.name = "SoulJadeRing";
            ring.color = ring.sprite != null ? Color.white : Color.clear;
            Vector2[] centers = {
                new Vector2(470,310), new Vector2(245,410), new Vector2(695,410),
                new Vector2(245,610), new Vector2(695,610), new Vector2(470,710)
            };
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int index = i;
                var p = centers[i];
                var button = Q.Button(design, "Socket_" + i, "", p.x - 58, p.y - 58, 116, 116,
                    () => SelectSlot(index), "souljade_socket");
                slotButtons[i] = button;
                slotIcons[i] = Q.Icon(button.transform, "jade_comma", 7, 7, 102, 102);
                slotIcons[i].name = "EquippedJade_" + i;
                SetJadeIcon(slotIcons[i], null);
                slotOutlines[i] = button.gameObject.AddComponent<Outline>();
                slotOutlines[i].effectDistance = new Vector2(1.5f, -1.5f);
                slotOutlines[i].enabled = false;
                emptySlotLabels[i] = Q.Text(button.transform, "EmptySocket", "＋", 22, 22, 72, 72, 36, Q.Muted);
                Q.Text(design, "SocketLabel_" + i, SlotNames[i], p.x - 58, p.y + 59, 116, 32, 22, Q.Muted);
            }

            detailName = Q.Text(design, "SelectedJadeName", "", 305, 410, 331, 42, 28);
            detailName.enableWordWrapping = false;
            detailRarity = Q.Text(design, "SelectedJadeRarity", "", 305, 456, 331, 32, 22, Q.Muted);
            detailRarity.enableWordWrapping = false;
            detailEffect = Q.Text(design, "SelectedJadeAffixes", "", 315, 498, 311, 140, 24, Q.Ink);
            detailEffect.lineSpacing = 2;
            detailEffect.enableWordWrapping = false;
            detailLore = Q.Text(design, "SelectedJadeLore", "", 300, 694, 341, 30, 23);
            detailLore.gameObject.SetActive(false);

            CompactInfoCard.Create(design, "SoulInventoryPanel", 50, 824, 841, 604);
            Q.Text(design, "InventoryTitle", "魂玉列表", 82, 844, 250, 42, 29, Q.Ink, TextAlignmentOptions.Left);
            BuildInventory();
            SetTopRect("OwnedJadeCount", 730, 865, 250, 36);
            ownedCountLabel.fontSize = 23;
            ownedCountLabel.color = Q.Muted;
            Q.Divider(design, 82, 900, 777);
            SetTopRect("OwnedJadeViewport", 470, 1158, 797, 484);
            ConfigureInventoryGrid();

            var equip = Q.Button(design, "ReplaceJade", "镶嵌", 80, 1454, 376, 80, EquipSelected);
            equipLabel = equip.GetComponentInChildren<TMP_Text>();
            Q.Button(design, "RemoveJade", "卸下", 485, 1454, 376, 80, RemoveSelected, "button_secondary");
            Q.Text(design, "InventoryScrollHint", "点击魂玉查看名称与词条", 150, 1554, 641, 32, 23, Q.Muted);
        }

        private void BuildQuietInkOperation(bool upgrade)
        {
            if (operationView != null) { operationView.SetActive(false); Destroy(operationView); }
            var owned = GetSelectedOwned();
            if (owned == null) return;
            operationIsUpgrade = upgrade;
            operationDesign = Q.Page(page.transform, upgrade ? "SoulJadeUpgradePage" : "SoulJadeRefinePage",
                upgrade ? "魂玉升级" : "魂玉洗练", CloseOperationView, out operationView);
            var definition = Jades[owned.JadeIndex];
            CompactInfoCard.Create(operationDesign, "OperationSummary", 55, 330, 831, 154);
            var icon = Q.Icon(operationDesign, "jade_comma", 88, 352, 108, 108);
            SetJadeIcon(icon, LoadIcon(definition.Icon));
            Q.Text(operationDesign, "OperationJadeName", definition.Name, 225, 353, 620, 46, 32, Q.Ink, TextAlignmentOptions.Left);
            Q.Text(operationDesign, "OperationCurrentLevel", "当前等级  " + owned.Level, 225, 414, 620, 34, 25, Q.Jade, TextAlignmentOptions.Left);
            CompactInfoCard.Create(operationDesign, "OperationDetails", 55, 518, 831, 324);
            Q.Text(operationDesign, "OperationHeading", "当前词条", 88, 542, 765, 42, 29, Q.Ink, TextAlignmentOptions.Left);
            Q.Divider(operationDesign, 88, 598, 765);
            for (int i = 0; i < 4; i++)
            {
                string value = i < owned.Affixes.Count
                    ? AffixNames[owned.Affixes[i].Key] + "  +" + owned.Affixes[i].Value.ToString("0.#") + "%"
                    : "空词条位";
                Q.Text(operationDesign, "CurrentAffix" + i, value, 91, 619 + i * 49, 759, 38, 27,
                    i < owned.Affixes.Count ? Q.Ink : Q.Muted, TextAlignmentOptions.Left);
            }
            Q.Text(operationDesign, "MissingService", "升级与洗练尚未开放\n当前仅可查看已保存的魂玉属性", 120, 878, 701, 82, 25, Q.Muted);
            Q.Button(operationDesign, "UnavailableAction", upgrade ? "升级暂未开放" : "洗练暂未开放",
                230, 1000, 481, 82, null, "button_disabled", 29);
            operationView.transform.SetAsLastSibling();
        }
    }
}
