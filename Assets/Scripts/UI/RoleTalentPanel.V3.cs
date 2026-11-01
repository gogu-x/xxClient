using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Q = XiuXian.Core.QuietInkUI;
namespace XiuXian.Core
{
    public sealed partial class RoleTalentPanel
    {
        public static string ArtTalentName(int branch,int tier){return Branches[Mathf.Clamp(branch,0,3)].talents[Mathf.Clamp(tier,0,23)];}
        public static string ArtTalentEffect(int branch,int tier){return Branches[Mathf.Clamp(branch,0,3)].effects[Mathf.Clamp(tier,0,23)];}
        public static bool ArtTalentLit(int branch,int tier){return tier==0||LoadSave().lit[NodeIndex(Mathf.Clamp(branch,0,3),Mathf.Clamp(tier,0,23))];}
        public static bool ArtUnlock(int branch,int tier)
        {
            if(branch<0||branch>=4||tier<=0||tier>=24)return false;
            var save=LoadSave();int index=NodeIndex(branch,tier);
            if(save.lit[index]||!HasPrerequisites(save,branch,tier)||AvailablePoints<=0)return false;
            save.lit[index]=true;PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(save));PlayerPrefs.Save();OnTalentsChanged?.Invoke();return true;
        }
        public static void ArtReset(){PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(new TalentSave()));PlayerPrefs.Save();OnTalentsChanged?.Invoke();}

        private readonly System.Collections.Generic.List<Image>[] quietPaths =
            new System.Collections.Generic.List<Image>[ConnectorCount];

        private void BuildQuietInkTalentPage()
        {
            var d = Q.Page(transform, PageName, "天赋", Close, out _page);
            Q.Text(d, "LocalTalentState", "本地天赋 · 未接入服务器", 150, 126, 641, 35, 24, Q.Muted);
            for (int branch = 0; branch < BranchCount; branch++)
            {
                int captured = branch;
                var tab = Q.Button(d, "SchoolTab" + branch, Branches[branch].name,
                    70 + branch * 200, 175, 200, 70, () => SelectBranch(captured), null, 30);
                _branchButtons[branch] = tab;
                _branchNames[branch] = tab.GetComponentInChildren<TMP_Text>();
                _branchImages[branch] = Q.Art(tab.transform, "tab_underline", 20, 62, 160, 14, true);
                _branchProgress[branch] = null;
            }
            Q.Divider(d, 55, 247, 831);
            _pointText = Q.Text(d, "PointSummary", "", 70, 264, 801, 38, 25, Q.Muted);

            var mountain = Q.Art(d, "character-scene", 30, 630, 881, 435);
            mountain.color = mountain.sprite != null ? new Color(1, 1, 1, .12f) : Color.clear;

            var content = Q.Scroll(d, "TalentTreeViewport", 65, 315, 811, 870, 2050);
            content.name = "SelectedSchoolTalentTree";
            _treeScroll = content.GetComponentInParent<ScrollRect>();
            Vector2[] positions = new Vector2[NodesPerBranch];
            positions[0] = new Vector2(405, 90);
            int[][] lanes = {
                new[] { 1, 4, 5, 10, 11, 16, 17, 18 },
                new[] { 2, 6, 7, 12, 13, 19, 20 },
                new[] { 3, 8, 9, 14, 15, 21, 22, 23 }
            };
            for (int lane = 0; lane < lanes.Length; lane++)
                for (int row = 0; row < lanes[lane].Length; row++)
                    positions[lanes[lane][row]] = new Vector2(135 + lane * 270, 310 + row * 220);
            for (int node = 1; node < NodesPerBranch; node++)
            {
                var lines = new System.Collections.Generic.List<Image>();
                quietPaths[node - 1] = lines;
                Vector2 from = positions[Parents[node]], to = positions[node];
                if (Parents[node] == 0)
                {
                    Vector2 direction = (to - from).normalized;
                    lines.Add(QuietPath(content, "TalentPath" + node, from + direction * 75, to - direction * 75));
                }
                else
                {
                    float laneX = to.x + (node % 2 == 0 ? -106 : 106);
                    float edge = laneX < to.x ? -72 : 72;
                    Vector2 start = from + new Vector2(edge, 0), finish = to + new Vector2(edge, 0);
                    lines.Add(QuietPath(content, "TalentPath" + node + "A", start, new Vector2(laneX, from.y)));
                    lines.Add(QuietPath(content, "TalentPath" + node + "B", new Vector2(laneX, from.y), new Vector2(laneX, to.y)));
                    lines.Add(QuietPath(content, "TalentPath" + node + "C", new Vector2(laneX, to.y), finish));
                }
                _connectorImages[node - 1] = lines[0];
            }
            for (int node = 0; node < NodesPerBranch; node++)
            {
                Vector2 p = positions[node];
                var button = Q.Button(content, "TalentNode" + node, "", p.x - 72, p.y - 72, 144, 144, null, "souljade_socket");
                button.interactable = true;
                _nodeButtons[node] = button;
                _nodeImages[node] = button.GetComponent<Image>();
                _nodeImages[node].sprite = Q.Sprite("talent_node") ?? Q.Sprite("souljade_socket");
                _nodeIcons[node] = Q.Icon(button.transform, "talent", 29, 27, 86, 86);
                _nodeOutlines[node] = button.gameObject.AddComponent<Outline>();
                _nodeOutlines[node].effectDistance = new Vector2(1.5f, -1.5f);
                _nodeLocks[node] = Q.Rect(button.transform, "LockState", 0, 0, 1, 1).gameObject.AddComponent<Image>();
                _nodeLocks[node].enabled = false;
                _nodeNames[node] = Q.Text(button.transform, "TalentName", "", -22, 148, 188, 42, 26);
                _nodeNames[node].enableWordWrapping = false;
                _nodeNames[node].enableAutoSizing = true;
                _nodeNames[node].fontSizeMin = 23;
                _nodeNames[node].fontSizeMax = 26;
                AddTalentPointerEvents(button.gameObject, node);
            }
            Q.Text(d, "TreeScrollHint", "上下滑动查看全部 24 个节点 · 点击查看详情", 100, 1198, 741, 35, 23, Q.Muted);

            var details = CompactInfoCard.Create(d, "TalentEffectDetails", 55, 1250, 831, 264);
            _detailsCard = details.gameObject;
            _detailsIconFrame = Q.Icon(details.transform, "souljade_socket", 28, 25, 104, 104);
            _detailsIconFrame.sprite = Q.Sprite("talent_node") ?? Q.Sprite("souljade_socket");
            _detailsIcon = Q.Icon(details.transform, "talent", 50, 46, 60, 60);
            _detailsTitle = Q.Text(details.transform, "TalentName", "", 158, 20, 641, 46, 34, Q.Ink, TextAlignmentOptions.Left);
            _detailsEffect = Q.Text(details.transform, "TalentEffect", "", 158, 71, 641, 52, 28, Q.Ink, TextAlignmentOptions.Left);
            _detailsState = Q.Text(details.transform, "TalentState", "", 158, 129, 641, 36, 24, Q.Muted, TextAlignmentOptions.Left);
            _learnButton = Q.Button(details.transform, "LearnSelected", "修习", 28, 178, 372, 72,
                () => { UnlockSelected(); Refresh(); ShowTalentDetails(_selectedBranch, _selectedTier); });
            Q.Button(details.transform, "ResetSelected", "重置", 431, 178, 372, 72, ShowResetDialog, "button_secondary");
            BuildQuietResetDialog();
            _selectedBranch = Mathf.Clamp(_selectedBranch, 0, BranchCount - 1);
            _selectedTier = Mathf.Clamp(_selectedTier == 0 ? 1 : _selectedTier, 0, NodesPerBranch - 1);
            Refresh();
            ShowTalentDetails(_selectedBranch, _selectedTier);
            _page.SetActive(false);
        }

        private static Image QuietPath(Transform parent, string name, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            var r = Q.Rect(parent, name, start.x, start.y, delta.magnitude, 2);
            r.pivot = new Vector2(0, .5f);
            r.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var line = r.gameObject.AddComponent<Image>();
            line.color = Q.LineColor;
            line.raycastTarget = false;
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
            Q.Text(d, "Message", "确认重置当前本地天赋？\n已分配的天赋点将返还。", 140, 695, 661, 88, 29);
            Q.Text(d, "Notice", "确认后将清空当前天赋分配", 140, 800, 661, 40, 25, Q.Muted);
            Q.Button(d, "CancelReset", "取消", 125, 895, 330, 82, () => _confirmDialog.SetActive(false), "button_secondary");
            Q.Button(d, "ConfirmReset", "确认", 485, 895, 330, 82, ResetTalents);
            _confirmDialog.SetActive(false);
        }
    }
}
