using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using XiuXian.Network;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    public sealed partial class RoleTalentPanel
    {
        public static string ArtTalentName(int branch, int node) { return DefinitionAt(branch, node)?.Name ?? "等待同步"; }
        public static string ArtTalentEffect(int branch, int node) { return DefinitionAt(branch, node)?.Effect ?? "效果待服务器同步"; }
        public static bool ArtTalentLit(int branch, int node)
        {
            var definition = DefinitionAt(branch, node);
            return definition != null && TalentService.IsLearned(definition.NodeId);
        }
        public static bool ArtUnlock(int branch, int node)
        {
            var definition = DefinitionAt(branch, node);
            return CanLearn(definition) && TalentService.Learn(definition.NodeId);
        }
        public static void ArtReset() { TalentService.Reset(); }

        private void BuildQuietInkTalentPage()
        {
            var d = Q.Page(transform, PageName, "天赋", Close, out _page);
            _serverStatus = Q.Text(d, "TalentServerState", TalentService.Status, 100, 126, 636, 38, 24, Q.Muted);
            _refreshButton = Q.Button(d, "RefreshTalents", "刷新", 756, 116, 130, 57,
                () => TalentService.RequestRefresh(), "button_secondary", 24);
            _branchRoot = Q.Rect(d, "TalentBranches", 70, 175, 800, 70);
            Q.Divider(d, 55, 247, 831);
            _pointText = Q.Text(d, "PointSummary", "", 70, 264, 801, 38, 25, Q.Muted);
            var mountain = Q.Art(d, "character-scene", 30, 630, 881, 435);
            mountain.color = mountain.sprite != null ? new Color(1, 1, 1, .12f) : Color.clear;
            _treeContent = Q.Scroll(d, "TalentTreeViewport", 65, 315, 811, 870, 870);
            _treeContent.name = "SelectedSchoolTalentTree";
            _treeScroll = _treeContent.GetComponentInParent<ScrollRect>();
            _treeHint = Q.Text(d, "TreeScrollHint", "", 100, 1198, 741, 35, 23, Q.Muted);
            var details = CompactInfoCard.Create(d, "TalentEffectDetails", 55, 1250, 831, 264);
            _detailsCard = details.gameObject;
            _detailsIconFrame = Q.Icon(details.transform, "talent_node", 28, 25, 104, 104);
            _detailsIcon = Q.Icon(details.transform, "talent", 50, 46, 60, 60);
            _detailsTitle = Q.Text(details.transform, "TalentName", "", 158, 20, 641, 46, 34, Q.Ink, TextAlignmentOptions.Left);
            _detailsEffect = Q.Text(details.transform, "TalentEffect", "", 158, 71, 641, 52, 28, Q.Ink, TextAlignmentOptions.Left);
            _detailsState = Q.Text(details.transform, "TalentState", "", 158, 129, 641, 36, 24, Q.Muted, TextAlignmentOptions.Left);
            _learnButton = Q.Button(details.transform, "LearnSelected", "修习", 28, 178, 372, 72, () => { UnlockSelected(); Refresh(); });
            _resetButton = Q.Button(details.transform, "ResetSelected", "重置", 431, 178, 372, 72, ShowResetDialog, "button_secondary");
            BuildQuietResetDialog();
            Refresh();
            _page.SetActive(false);
        }
        private void RefreshDirectoryLayout()
        {
            var branchKey = new StringBuilder();
            foreach (var branch in _branches) branchKey.Append(branch.BranchId).Append(':').Append(branch.Name).Append(':').Append(branch.IconKey).Append(';');
            if (_branchSignature != branchKey.ToString())
            {
                _branchSignature = branchKey.ToString();
                ClearChildren(_branchRoot);
                _branchButtons.Clear();
                float width = 800f / Mathf.Max(1, _branches.Count);
                for (int i = 0; i < _branches.Count; i++)
                {
                    int index = i;
                    var branch = _branches[i];
                    var tab = Q.Button(_branchRoot, "SchoolTab" + i, branch.Name, i * width, 0, width, 70,
                        () => SelectBranch(index), null, 30);
                    if (!string.IsNullOrEmpty(branch.IconKey)) Q.Icon(tab.transform, branch.IconKey, 8, 20, 30, 30);
                    _branchButtons.Add(tab);
                }
            }
            var treeKey = new StringBuilder().Append(_selectedBranch).Append('|');
            foreach (var node in _nodes)
                treeKey.Append(node.NodeId).Append(':').Append(node.ParentId).Append(':').Append(node.UiIndex).Append(':')
                    .Append(node.IsRoot).Append(':').Append(node.Name).Append(':').Append(node.IconKey).Append(';');
            if (_treeSignature == treeKey.ToString()) return;
            _treeSignature = treeKey.ToString();
            RebuildTalentTree();
        }
        private void RebuildTalentTree()
        {
            ClearChildren(_treeContent);
            _nodeViews.Clear(); _edges.Clear();
            var definitions = new Dictionary<int, TalentNodeDefinition>();
            var positions = new Dictionary<int, Vector2>();
            foreach (var node in _nodes) definitions[node.NodeId] = node;
            var firstChildren = new List<int>();
            foreach (var node in _nodes)
            {
                TalentNodeDefinition parent;
                if (definitions.TryGetValue(node.ParentId, out parent) && (parent.IsRoot || parent.ParentId < 0))
                    firstChildren.Add(node.NodeId);
            }
            int[] laneRows = { 0, 0, 0 };
            float bottom = 650;
            int rootCount = _nodes.FindAll(n => n.IsRoot || n.ParentId < 0).Count;
            int rootIndex = 0;
            foreach (var node in _nodes)
            {
                Vector2 position;
                if (node.IsRoot || node.ParentId < 0)
                    position = new Vector2(811f * (++rootIndex) / (rootCount + 1), 90);
                else
                {
                    var ancestor = node;
                    var visited = new HashSet<int>();
                    TalentNodeDefinition parent;
                    while (visited.Add(ancestor.NodeId) && definitions.TryGetValue(ancestor.ParentId, out parent)
                        && !parent.IsRoot && parent.ParentId >= 0) ancestor = parent;
                    int lane = firstChildren.IndexOf(ancestor.NodeId);
                    lane = lane < 0 ? 1 : lane % laneRows.Length;
                    position = new Vector2(135 + lane * 270, 310 + laneRows[lane]++ * 220);
                }
                positions[node.NodeId] = position;
                bottom = Mathf.Max(bottom, position.y + 220);
            }
            _treeContent.sizeDelta = new Vector2(811, Mathf.Max(870, bottom));
            foreach (var node in _nodes)
            {
                Vector2 start, end;
                if (!positions.TryGetValue(node.ParentId, out start) || !positions.TryGetValue(node.NodeId, out end)) continue;
                var edge = new EdgeView { Child = node };
                var parent = definitions[node.ParentId];
                if (parent.IsRoot || parent.ParentId < 0)
                {
                    Vector2 direction = (end - start).normalized;
                    edge.Lines.Add(QuietPath(_treeContent, "TalentPath" + node.NodeId, start + direction * 75, end - direction * 75));
                }
                else
                {
                    float laneX = end.x + (node.UiIndex % 2 == 0 ? -106 : 106);
                    float side = laneX < end.x ? -72 : 72;
                    edge.Lines.Add(QuietPath(_treeContent, "TalentPath" + node.NodeId + "A", start + new Vector2(side, 0), new Vector2(laneX, start.y)));
                    edge.Lines.Add(QuietPath(_treeContent, "TalentPath" + node.NodeId + "B", new Vector2(laneX, start.y), new Vector2(laneX, end.y)));
                    edge.Lines.Add(QuietPath(_treeContent, "TalentPath" + node.NodeId + "C", new Vector2(laneX, end.y), end + new Vector2(side, 0)));
                }
                _edges.Add(edge);
            }
            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                var position = positions[node.NodeId];
                var button = Q.Button(_treeContent, "TalentNode" + i, "", position.x - 72, position.y - 72, 144, 144, null, "souljade_socket");
                button.interactable = true;
                var frame = button.GetComponent<Image>();
                frame.sprite = Q.Sprite("talent_node") ?? Q.Sprite("souljade_socket");
                var highlight = Q.Rect(button.transform, "ActivationHighlight", -20, -20, 184, 184).gameObject.AddComponent<TalentNodeHighlight>();
                highlight.raycastTarget = false;
                var icon = Q.Icon(button.transform, "talent", 29, 27, 86, 86);
                icon.sprite = TalentIcon(node);
                var name = Q.Text(button.transform, "TalentName", node.Name, -22, 148, 188, 42, 26);
                name.enableWordWrapping = false; name.enableAutoSizing = true; name.fontSizeMin = 23; name.fontSizeMax = 26;
                _nodeViews.Add(new NodeView { Button = button, Frame = frame, Icon = icon, Name = name, Highlight = highlight });
                AddTalentPointerEvents(button.gameObject, i);
            }
        }
        private static void ClearChildren(Transform root)
        {
            foreach (Transform child in root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }
        private static Image QuietPath(Transform parent, string name, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            var rect = Q.Rect(parent, name, start.x, start.y, delta.magnitude, 2);
            rect.pivot = new Vector2(0, .5f);
            rect.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var line = rect.gameObject.AddComponent<Image>();
            line.color = Q.LineColor; line.raycastTarget = false;
            return line;
        }
        private void BuildQuietResetDialog()
        {
            var r = Q.Rect(_page.transform, "ResetConfirmation", 0, 0, 0, 0);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            _confirmDialog = r.gameObject;
            r.gameObject.AddComponent<Image>().color = new Color(.12f, .18f, .16f, .58f);
            var d = Q.Design(r);
            CompactInfoCard.Create(d, "ResetPanel", 80, 575, 781, 450);
            Q.Text(d, "Title", "重置天赋", 170, 610, 601, 62, 38);
            Q.Text(d, "Message", "确认重置全部天赋？\n已分配的天赋点将返还。", 140, 695, 661, 88, 29);
            Q.Text(d, "Notice", "全部流派将重置，已开放核心保留", 140, 800, 661, 40, 25, Q.Muted);
            Q.Button(d, "CancelReset", "取消", 125, 895, 330, 82, () => _confirmDialog.SetActive(false), "button_secondary");
            Q.Button(d, "ConfirmReset", "确认", 485, 895, 330, 82, ResetTalents);
            _confirmDialog.SetActive(false);
        }
    }
}
