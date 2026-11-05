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
            serverStatusLabel = Q.Text(design, "ServerState", "登录后同步魂玉", 145, 116, 595, 48, 22, Q.Muted);
            syncButton = Q.Button(design, "RefreshSoulJades", "刷新", 750, 113, 130, 48, ReloadSoulJades, "button_secondary", 23);
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

            equipButton = Q.Button(design, "ReplaceJade", "镶嵌", 80, 1454, 376, 80, EquipSelected);
            equipLabel = equipButton.GetComponentInChildren<TMP_Text>();
            removeButton = Q.Button(design, "RemoveJade", "卸下", 485, 1454, 376, 80, RemoveSelected, "button_secondary");
            Q.Text(design, "InventoryScrollHint", "通用槽位 · 同一魂玉只能镶嵌一次", 100, 1554, 741, 32, 23, Q.Muted);
        }

        private void BuildQuietInkOperation(bool upgrade)
        {
            if (operationView != null) { operationView.SetActive(false); Destroy(operationView); }
            var owned = GetSelectedOwned();
            if (owned == null) return;
            operationIsUpgrade = upgrade;
            operationDesign = Q.Page(page.transform, upgrade ? "SoulJadeUpgradePage" : "SoulJadeRefinePage",
                upgrade ? "魂玉升级" : "魂玉洗练", CloseOperationView, out operationView);
            CompactInfoCard.Create(operationDesign, "OperationSummary", 55, 330, 831, 154);
            var icon = Q.Icon(operationDesign, "jade_comma", 88, 352, 108, 108);
            SetJadeIcon(icon, OwnedIcon(owned));
            Q.Text(operationDesign, "OperationJadeName", owned.Name, 225, 353, 620, 46, 32, Q.Ink, TextAlignmentOptions.Left);
            Q.Text(operationDesign, "OperationCurrentLevel", "当前等级  " + owned.Level + " / " + owned.MaxLevel,
                225, 414, 620, 34, 25, Q.Jade, TextAlignmentOptions.Left);
            CompactInfoCard.Create(operationDesign, "OperationDetails", 55, 518, 831, 324);
            Q.Text(operationDesign, "OperationHeading", "当前词条", 88, 542, 765, 42, 29, Q.Ink, TextAlignmentOptions.Left);
            Q.Divider(operationDesign, 88, 598, 765);
            for (int i = 0; i < owned.Affixes.Count; i++)
            {
                string value = i < owned.Affixes.Count
                    ? FormatAffix(owned.Affixes[i])
                    : "空词条位";
                Q.Text(operationDesign, "CurrentAffix" + i, value, 91, 619 + i * 49, 759, 38, 27,
                    i < owned.Affixes.Count ? Q.Ink : Q.Muted, TextAlignmentOptions.Left);
            }
            long essence = soulService != null && soulService.Data != null ? soulService.Data.Essence : 0;
            long cost = upgrade ? owned.UpgradeCost : owned.RefineCost;
            bool atMax = upgrade && owned.Level >= owned.MaxLevel;
            bool ready = soulService != null && soulService.CanMutate;
            Q.Text(operationDesign, "OperationRule", upgrade ? "提升魂玉等级，增强当前词条" : "重新生成词条，替换当前属性",
                110, 878, 721, 40, 25, Q.Muted);
            Q.Text(operationDesign, "EssenceBalance", "玉髓  " + essence.ToString("N0"), 110, 938, 721, 42, 28, Q.Jade);
            Q.Text(operationDesign, "OperationCost", atMax ? "已达最高等级" : "消耗玉髓  " + cost.ToString("N0"),
                110, 992, 721, 38, 24, essence >= cost ? Q.Muted : Q.Cinnabar);
            string actionLabel = soulService != null && soulService.IsBusy ? "处理中…" : atMax ? "已满级" :
                essence < cost ? "玉髓不足" : upgrade ? "确认升级" : "确认洗练";
            var confirm = Q.Button(operationDesign, "ConfirmSoulJadeOperation", actionLabel, 230, 1070, 481, 82,
                ConfirmSoulJadeOperation, "button_primary", 29);
            confirm.interactable = ready && !atMax && essence >= cost;
            Q.Text(operationDesign, "OperationStatus", soulService != null ? soulService.Status : "请先同步魂玉",
                100, 1180, 741, 78, 24, Q.Muted);
            var refresh = Q.Button(operationDesign, "RefreshOperation", "刷新", 365, 1280, 211, 60,
                ReloadSoulJades, "button_secondary", 24);
            refresh.interactable = soulService != null && !soulService.IsBusy;
            operationView.transform.SetAsLastSibling();
        }
    }
}
