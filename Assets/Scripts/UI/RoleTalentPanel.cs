using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XiuXian.Network;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    /// <summary>Renders the server directory; ownership, prerequisites and costs are authoritative.</summary>
    public sealed partial class RoleTalentPanel : MonoBehaviour
    {
        private const string PageName = "RoleTalentTreeV6";
        private const string RankKey = "CultivationRealmRank";
        private const float DoubleClickWindow = .46f;
        private GameObject _page, _detailsCard, _confirmDialog;
        private RectTransform _branchRoot, _treeContent;
        private ScrollRect _treeScroll;
        private Button _learnButton, _resetButton, _refreshButton;
        private TMP_Text _pointText, _serverStatus, _detailsTitle, _detailsEffect, _detailsState, _treeHint;
        private Image _detailsIconFrame, _detailsIcon;
        private readonly List<Button> _branchButtons = new List<Button>();
        private readonly List<NodeView> _nodeViews = new List<NodeView>();
        private readonly List<EdgeView> _edges = new List<EdgeView>();
        private List<TalentBranchDefinition> _branches = new List<TalentBranchDefinition>();
        private List<TalentNodeDefinition> _nodes = new List<TalentNodeDefinition>();
        private int _selectedBranch, _selectedTier;
        private int _lastClickBranch = -1, _lastClickTier = -1;
        private float _lastClickAt = -1;
        private string _branchSignature, _treeSignature;

        private sealed class NodeView
        {
            public Button Button;
            public Image Frame, Icon;
            public TMP_Text Name;
            public TalentNodeHighlight Highlight;
        }
        private sealed class EdgeView
        {
            public TalentNodeDefinition Child;
            public readonly List<Image> Lines = new List<Image>();
        }
        [Serializable]
        public struct TalentBonuses
        {
            public float attackPercent, defensePercent, healthPercent, skillDamagePercent;
            public float attackSpeedPercent, speedPercent, criticalRatePercent, criticalDamagePercent;
            public float dodgePercent, damageReductionPercent, healingPercent, lootPercent;
            public float cooldownReductionPercent, manaRecoveryPercent;
        }
        public static event Action OnTalentsChanged
        {
            add => TalentService.Changed += value;
            remove => TalentService.Changed -= value;
        }

        // The separate local cultivation preview does not grant server talent points.
        public static int CultivationRank => Mathf.Clamp(PlayerPrefs.GetInt(RankKey, 6), 1, 20);
        public static void SetCultivationRank(int rank)
        {
            PlayerPrefs.SetInt(RankKey, Mathf.Clamp(rank, 1, 20));
            PlayerPrefs.Save();
        }
        public static int EarnedPoints => TalentService.Data?.EarnedPoints ?? 0;
        public static int SpentPoints => TalentService.Data?.SpentPoints ?? 0;
        public static int AvailablePoints => TalentService.Data?.AvailablePoints ?? 0;
        public static TalentBonuses GetBonuses()
        {
            var value = TalentService.Data?.Bonuses;
            return value == null ? new TalentBonuses() : new TalentBonuses
            {
                healthPercent = value.MaxHpPermille / 10f, attackPercent = value.AttackPermille / 10f,
                defensePercent = value.DefensePermille / 10f, speedPercent = value.SpeedPermille / 10f
            };
        }
        private void Awake()
        {
            BuildQuietInkTalentPage();
            TalentService.Changed += OnServerTalentChanged;
        }
        private void OnDisable() { Close(); }
        private void OnDestroy() { TalentService.Changed -= OnServerTalentChanged; }
        private void OnServerTalentChanged() { if (_page != null) Refresh(); }
        public void Open()
        {
            if (_page == null) BuildQuietInkTalentPage();
            _confirmDialog.SetActive(false);
            _page.SetActive(true);
            var selection = transform.Find(CharacterPanel.PageRootName);
            if (selection != null) selection.gameObject.SetActive(false);
            _page.transform.SetAsLastSibling();
            Refresh();
            TalentService.RequestRefresh();
        }
        private static List<TalentBranchDefinition> BranchDirectory()
        {
            var result = TalentService.Data == null ? new List<TalentBranchDefinition>()
                : new List<TalentBranchDefinition>(TalentService.Data.Branches);
            result.Sort((a, b) => a.BranchId.CompareTo(b.BranchId));
            return result;
        }
        private static List<TalentNodeDefinition> NodeDirectory(int branchIndex)
        {
            var result = new List<TalentNodeDefinition>();
            var branches = BranchDirectory();
            if (TalentService.Data == null || branchIndex < 0 || branchIndex >= branches.Count) return result;
            int branchId = branches[branchIndex].BranchId;
            foreach (var node in TalentService.Data.Nodes) if (node.BranchId == branchId) result.Add(node);
            result.Sort((a, b) => a.UiIndex != b.UiIndex ? a.UiIndex.CompareTo(b.UiIndex) : a.NodeId.CompareTo(b.NodeId));
            return result;
        }
        private static TalentNodeDefinition DefinitionAt(int branchIndex, int nodeIndex)
        {
            var nodes = NodeDirectory(branchIndex);
            return nodeIndex >= 0 && nodeIndex < nodes.Count ? nodes[nodeIndex] : null;
        }
        private static bool HasPrerequisites(TalentNodeDefinition node)
        {
            return node != null && (node.ParentId < 0 || TalentService.IsLearned(node.ParentId));
        }
        private static bool CanLearn(TalentNodeDefinition node)
        {
            return node != null && TalentService.CanWrite && !node.IsRoot && !TalentService.IsLearned(node.NodeId)
                && HasPrerequisites(node) && node.PointCost >= 0 && AvailablePoints >= node.PointCost;
        }
        private void SelectBranch(int branchIndex)
        {
            if (branchIndex < 0 || branchIndex >= _branches.Count) return;
            _selectedBranch = branchIndex;
            _selectedTier = 0;
            int first = NodeDirectory(branchIndex).FindIndex(n => !n.IsRoot);
            if (first >= 0) _selectedTier = first;
            _lastClickBranch = _lastClickTier = -1;
            _lastClickAt = -1;
            Refresh();
            if (_treeScroll != null) _treeScroll.verticalNormalizedPosition = 1;
        }
        private void Refresh()
        {
            _branches = BranchDirectory();
            _selectedBranch = Mathf.Clamp(_selectedBranch, 0, Mathf.Max(0, _branches.Count - 1));
            _nodes = NodeDirectory(_selectedBranch);
            _selectedTier = Mathf.Clamp(_selectedTier, 0, Mathf.Max(0, _nodes.Count - 1));
            RefreshDirectoryLayout();
            _pointText.text = TalentService.HasData
                ? $"天赋点 {EarnedPoints}  ·  已用 {SpentPoints} 点  ·  可用 {AvailablePoints} 点" : "天赋点等待同步";
            _serverStatus.text = TalentService.Status;
            _refreshButton.interactable = !TalentService.IsPending;
            _resetButton.interactable = TalentService.CanWrite && SpentPoints > 0;
            _treeHint.text = _nodes.Count > 0 ? "上下滑动查看全部 " + _nodes.Count + " 个节点 · 点击查看详情" : "等待服务器同步天赋目录";
            for (int i = 0; i < _branchButtons.Count; i++)
                _branchButtons[i].GetComponentInChildren<TMP_Text>().color = i == _selectedBranch ? Q.Jade : Q.Muted;
            for (int i = 0; i < _nodeViews.Count; i++)
            {
                var view = _nodeViews[i];
                var node = _nodes[i];
                bool lit = TalentService.IsLearned(node.NodeId), ready = CanLearn(node);
                view.Frame.color = lit ? new Color(1, .95f, .75f) : ready ? new Color(.86f, .90f, .85f, .85f) : new Color(.70f, .74f, .70f, .58f);
                view.Icon.color = lit ? Color.white : ready ? new Color(.79f, .84f, .80f, .75f) : new Color(.75f, .78f, .75f, .36f);
                view.Name.color = lit ? Q.Jade : ready ? Q.Ink : Q.Muted;
                view.Name.fontStyle = lit ? FontStyles.Bold : FontStyles.Normal;
                view.Highlight.SetState(lit, i == _selectedTier, ready);
            }
            foreach (var edge in _edges)
            {
                bool lit = TalentService.IsLearned(edge.Child.NodeId), connected = HasPrerequisites(edge.Child);
                foreach (var line in edge.Lines)
                {
                    line.color = lit ? new Color(.79f, .55f, .20f) : connected ? Q.Jade : new Color(.77f, .78f, .73f, .55f);
                    line.rectTransform.sizeDelta = new Vector2(line.rectTransform.sizeDelta.x, lit ? 5 : connected ? 2.5f : 1.5f);
                }
            }
            ShowTalentDetails(_selectedBranch, _selectedTier);
        }
        private void ShowTalentDetails(int branchIndex, int nodeIndex)
        {
            if (_detailsCard == null) return;
            var node = DefinitionAt(branchIndex, nodeIndex);
            bool lit = node != null && TalentService.IsLearned(node.NodeId);
            _detailsTitle.text = node == null ? "请选择天赋" : node.Name;
            _detailsEffect.text = node == null ? "效果待服务器同步" : node.Effect;
            _detailsState.text = node == null ? "等待天赋数据同步" : TalentService.IsPending ? "正在同步，请稍候…"
                : lit ? (node.IsRoot ? "主脉核心 · 已开启" : "已点亮 · 下场战斗生效")
                : node.IsRoot ? "等待服务器开启" : !HasPrerequisites(node) ? "先点亮前置节点后可解锁"
                : !TalentService.CanWrite ? "请刷新确认天赋状态" : AvailablePoints < node.PointCost ? "可用天赋点不足"
                : node.PointCost == 0 ? "双击节点免费点亮" : "双击节点点亮 · 消耗 " + node.PointCost + " 点";
            _detailsState.color = lit ? Q.Jade : Q.Muted;
            _detailsIcon.sprite = TalentIcon(node);
            _detailsIconFrame.color = lit ? new Color(1, .95f, .75f) : Color.white;
            _learnButton.interactable = CanLearn(node);
            _learnButton.GetComponentInChildren<TMP_Text>().text = lit ? "已点亮" : "修习";
            _detailsCard.SetActive(true);
        }
        private bool UnlockSelected()
        {
            var node = DefinitionAt(_selectedBranch, _selectedTier);
            return CanLearn(node) && TalentService.Learn(node.NodeId);
        }
        private void AddTalentPointerEvents(GameObject target, int index)
        {
            var trigger = target.AddComponent<EventTrigger>();
            foreach (var eventType in new[] { EventTriggerType.InitializePotentialDrag, EventTriggerType.BeginDrag,
                EventTriggerType.Drag, EventTriggerType.EndDrag, EventTriggerType.Scroll })
            {
                var type = eventType;
                var entry = new EventTrigger.Entry { eventID = type };
                entry.callback.AddListener(data =>
                {
                    var pointer = data as PointerEventData;
                    if (_treeScroll == null || pointer == null) return;
                    switch (type)
                    {
                        case EventTriggerType.InitializePotentialDrag: _treeScroll.OnInitializePotentialDrag(pointer); break;
                        case EventTriggerType.BeginDrag: _lastClickAt = -1; _treeScroll.OnBeginDrag(pointer); break;
                        case EventTriggerType.Drag: _treeScroll.OnDrag(pointer); break;
                        case EventTriggerType.EndDrag: _treeScroll.OnEndDrag(pointer); break;
                        case EventTriggerType.Scroll: _treeScroll.OnScroll(pointer); break;
                    }
                });
                trigger.triggers.Add(entry);
            }
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(data =>
            {
                var pointer = data as PointerEventData;
                bool repeat = _selectedBranch == _lastClickBranch && index == _lastClickTier && Time.unscaledTime - _lastClickAt <= DoubleClickWindow;
                _selectedTier = index;
                if (repeat || (pointer != null && pointer.clickCount >= 2)) { _lastClickAt = -1; UnlockSelected(); }
                else { _lastClickBranch = _selectedBranch; _lastClickTier = index; _lastClickAt = Time.unscaledTime; }
                Refresh();
            });
            trigger.triggers.Add(click);
        }
        private void ShowResetDialog()
        {
            if (!TalentService.CanWrite || SpentPoints == 0) return;
            _confirmDialog.SetActive(true);
            _confirmDialog.transform.SetAsLastSibling();
        }
        private void ResetTalents() { if (TalentService.Reset()) _confirmDialog.SetActive(false); }
        private void Close()
        {
            bool wasOpen = _page != null && _page.activeSelf;
            _lastClickAt = -1;
            _lastClickBranch = _lastClickTier = -1;
            if (_confirmDialog != null) _confirmDialog.SetActive(false);
            if (_page != null) _page.SetActive(false);
            if (wasOpen && isActiveAndEnabled)
            {
                var character = GetComponent<CharacterPanel>();
                if (character != null) character.ShowAttributes();
            }
        }
        private static Sprite TalentIcon(TalentNodeDefinition node)
        {
            return node == null || string.IsNullOrEmpty(node.IconKey) ? Q.Sprite("talent") : Q.Sprite(node.IconKey) ?? Q.Sprite("talent");
        }
    }
}
