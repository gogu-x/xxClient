using UnityEngine;
using XiuXian.Network;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    public sealed partial class RoleSoulJadePanel
    {
        private void BindService()
        {
            if (serviceBound) return;
            soulService = SoulJadeService.Instance;
            soulService.Changed += ApplyServerState;
            soulService.OperationCompleted += OnOperationCompleted;
            serviceBound = true;
        }

        private void UnbindService()
        {
            if (serviceBound && soulService != null)
            {
                soulService.Changed -= ApplyServerState;
                soulService.OperationCompleted -= OnOperationCompleted;
            }
            serviceBound = false;
        }

        private void ReloadSoulJades()
        {
            BindService();
            soulService.Reload();
        }

        private void ApplyServerState()
        {
            bool rebuildOperation = operationView != null && operationView.activeSelf;
            string selectedId = GetSelectedOwned()?.InstanceId;
            bool firstSnapshot = ownedJades.Count == 0;
            ownedJades.Clear();
            for (int i = 0; i < equipped.Length; i++) { equipped[i] = -1; visibleSlotIds[i] = -1; }
            var data = soulService != null ? soulService.Data : null;
            if (data != null)
            {
                foreach (var serverItem in data.Items)
                {
                    if (string.IsNullOrEmpty(serverItem.InstanceId)) continue;
                    int iconIndex;
                    string iconSuffix = serverItem.IconKey.StartsWith("soul_jade_") ? serverItem.IconKey.Substring(10) : "";
                    if (!int.TryParse(iconSuffix, out iconIndex)) iconIndex = 0;
                    var item = new OwnedJade
                    {
                        InstanceId = serverItem.InstanceId,
                        ConfigId = serverItem.ConfigId,
                        JadeIndex = iconIndex,
                        Name = string.IsNullOrEmpty(serverItem.Name) ? "未命名魂玉" : serverItem.Name,
                        Rarity = serverItem.Rarity,
                        IconKey = serverItem.IconKey,
                        Level = serverItem.Level,
                        MaxLevel = serverItem.MaxLevel,
                        UpgradeCost = serverItem.UpgradeCost,
                        RefineCost = serverItem.RefineCost
                    };
                    foreach (var affix in serverItem.Affixes)
                    {
                        int key = affix.Attribute == 1001 ? 4 : affix.Attribute == 1002 ? 5 : affix.Attribute - 1;
                        if (key < 0 || key >= AffixNames.Length) continue;
                        item.Affixes.Add(new JadeAffix { Key = key, Value = affix.Permille / 10f });
                    }
                    ownedJades.Add(item);
                }
                var slots = new System.Collections.Generic.List<global::SoulJadeSlot>(data.Slots);
                slots.Sort((left, right) => left.Slot.CompareTo(right.Slot));
                for (int i = 0; i < slots.Count && i < equipped.Length; i++)
                {
                    visibleSlotIds[i] = slots[i].Slot;
                    equipped[i] = ownedJades.FindIndex(j => j.InstanceId == slots[i].InstanceId);
                }
            }
            itemSelected = string.IsNullOrEmpty(selectedId) ? -1 : ownedJades.FindIndex(j => j.InstanceId == selectedId);
            if (firstSnapshot && itemSelected < 0) itemSelected = equipped[slotSelected];
            if (page == null) return;
            RefreshSlots();
            RefreshInventory();
            RefreshDetail();
            if (rebuildOperation)
            {
                if (GetSelectedOwned() == null) CloseOperationView();
                else BuildQuietInkOperation(operationIsUpgrade);
            }
        }

        private void OnOperationCompleted(bool success, string message)
        {
            if (page != null && page.activeInHierarchy) Toast(message);
        }

        private void ConfirmSoulJadeOperation()
        {
            var selected = GetSelectedOwned();
            if (selected == null) return;
            BindService();
            bool sent = operationIsUpgrade ? soulService.Upgrade(selected.InstanceId) : soulService.Refine(selected.InstanceId);
            if (!sent) Toast(soulService.Status);
        }

        private static Sprite OwnedIcon(OwnedJade item)
        {
            if (!string.IsNullOrEmpty(item.IconKey))
            {
                if (SoulJadeIcons.TryGetValue(item.IconKey, out var cached) && cached != null) return cached;
                foreach (var sprite in Resources.LoadAll<Sprite>(SoulJadeIconAtlas)) SoulJadeIcons[sprite.name] = sprite;
                if (SoulJadeIcons.TryGetValue(item.IconKey, out cached) && cached != null) return cached;
                var icon = Q.Sprite(item.IconKey);
                if (icon != null) return icon;
            }
            return Q.Sprite("jade_comma");
        }
    }
}
