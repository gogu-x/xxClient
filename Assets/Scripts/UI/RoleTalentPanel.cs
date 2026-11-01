using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    /// <summary>
    /// Builds the four-school branching talent tree from modular UI sprites.
    /// Progress and talent bonuses remain compatible with the previous save format.
    /// </summary>
    public sealed partial class RoleTalentPanel : MonoBehaviour
    {
        private const string PageName = "RoleTalentTreeV6";
        private const string SaveKey = "RoleTalentTreeV3";
        private const string PreviousSaveKey = "RoleTalentTreeV2";
        private const string LegacySaveKey = "RoleBaguaTalentTreeV1";
        private const string RankKey = "CultivationRealmRank";
        private const string ArtPath = "UI/TalentTreeConcept_v3/";
        private const int BranchCount = 4;
        private const int NodesPerBranch = 24;
        private const int NodeCount = BranchCount * NodesPerBranch;
        private const int ConnectorCount = NodesPerBranch - 1;
        private const float DoubleClickWindow = 0.46f;

        private static readonly Color Gold = CelestialTheme.Gold;
        private static readonly Color PaleJade = new(0.77f, 0.94f, 0.85f, 1f);
        private static readonly Color MainText = CelestialTheme.Light;
        private static readonly string[] RealmNames =
        {
            "炼气初期", "炼气中期", "炼气后期", "炼气圆满", "筑基初期",
            "筑基中期", "筑基后期", "金丹初期", "金丹中期", "金丹后期",
            "元婴初期", "元婴中期", "元婴后期", "化神初期", "化神中期",
            "化神后期", "合体初期", "合体后期", "渡劫期", "大乘期"
        };

        private static readonly int[] Parents = { -1, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 5, 6, 7, 8, 9, 10, 10, 11, 12, 13, 14, 14, 15 };
        private static readonly int[] IconIndices = { 0, 4, 1, 3, 2, 5, 6, 7, 0, 3, 1, 4, 2, 5, 6, 7, 0, 3, 1, 4, 2, 5, 6, 7 };
        private static readonly Branch[] Branches =
        {
            new("剑", "破军", "锋芒破阵，专精攻击与暴击", new Color(0.96f, 0.72f, 0.42f),
                new[] { "剑心初醒", "裂空", "追击", "破甲", "碎星", "剑气", "锋锐", "绝影", "乱刃", "斩星", "穿云", "回风", "破军", "刃舞", "追魂", "剑域", "天锋", "斩岳", "碎星诀", "杀心", "剑魄", "万剑", "归宗", "斩天" },
                new[] { "主脉基础节点（无需消耗）", "攻击 +4%", "暴击率 +1%", "攻击 +2%", "攻击 +4%", "暴击伤害 +3%", "攻击 +3%", "攻速 +2%", "攻击 +3%", "暴击伤害 +4%", "攻击 +4%", "暴击率 +2%", "攻速 +3%", "攻击 +4%", "暴击率 +2%", "暴击伤害 +4%", "攻击 +5%", "攻速 +3%", "暴击率 +3%", "攻击 +5%", "暴击伤害 +5%", "攻击 +6%", "攻速 +4%", "暴击伤害 +8%" }),
            new("灵", "御灵", "引气化灵，强化术法与灵力", new Color(0.52f, 0.83f, 0.91f),
                new[] { "灵泉初涌", "潮生", "凝神", "回气", "引潮", "聚灵", "水行", "静心", "灵涌", "玄水", "术法精通", "灵息", "灵脉", "清心诀", "通玄", "引雷", "灵台", "五行", "气海", "结界", "归元", "太虚", "万法", "万法归元" },
                new[] { "主脉基础节点（无需消耗）", "技能伤害 +3%", "灵力回复 +3%", "冷却 -1%", "技能伤害 +3%", "灵力回复 +4%", "技能伤害 +3%", "冷却 -1%", "灵力回复 +4%", "技能伤害 +4%", "灵力回复 +5%", "冷却 -2%", "技能伤害 +4%", "灵力回复 +5%", "技能伤害 +5%", "冷却 -2%", "灵力回复 +6%", "技能伤害 +5%", "灵力回复 +6%", "冷却 -3%", "技能伤害 +6%", "灵力回复 +7%", "技能伤害 +7%", "冷却 -5%" }),
            new("山", "守御", "厚土护身，提升气血与承伤", new Color(0.75f, 0.72f, 0.55f),
                new[] { "磐石心诀", "固本", "护体", "守心", "厚土", "回春", "卸劲", "铁骨", "生息", "坚韧", "山岳", "金刚", "守御", "不动", "玄机", "镇岳", "护体金身", "长生", "固魂", "玄武", "地脉", "灵甲", "不坏", "玄武镇岳" },
                new[] { "主脉基础节点（无需消耗）", "防御 +2%", "受伤降低 +1%", "生命 +2%", "防御 +3%", "生命 +3%", "受伤降低 +2%", "治疗效果 +3%", "防御 +3%", "生命 +4%", "受伤降低 +3%", "防御 +4%", "生命 +4%", "受伤降低 +4%", "治疗效果 +4%", "生命 +5%", "防御 +5%", "受伤降低 +5%", "生命 +5%", "治疗效果 +5%", "防御 +6%", "生命 +6%", "受伤降低 +6%", "生命 +10%" }),
            new("风", "身法", "御风而行，强化速度与闪避", new Color(0.51f, 0.88f, 0.73f),
                new[] { "轻身诀", "逐风", "灵步", "先机", "踏雪", "回身", "疾行", "流云", "闪转", "逍遥游", "风息", "掠影", "踏虚", "乘风", "风遁", "天涯步", "幻影", "踏星", "追风", "化影", "凌波", "御风", "无痕", "天涯咫尺" },
                new[] { "主脉基础节点（无需消耗）", "闪避 +2%", "移动速度 +2%", "攻速 +2%", "闪避 +3%", "冷却 -1%", "攻速 +3%", "闪避 +3%", "移动速度 +3%", "攻速 +4%", "闪避 +4%", "冷却 -2%", "攻速 +4%", "闪避 +5%", "移动速度 +4%", "攻速 +5%", "闪避 +5%", "冷却 -3%", "移动速度 +5%", "闪避 +6%", "攻速 +6%", "闪避 +7%", "移动速度 +6%", "冷却 -5%" })
        };
        [Serializable]
        private sealed class TalentSave
        {
            public int version = 3;
            public bool[] lit = new bool[NodeCount];
        }

        [Serializable]
        public struct TalentBonuses
        {
            public float attackPercent;
            public float defensePercent;
            public float healthPercent;
            public float skillDamagePercent;
            public float attackSpeedPercent;
            public float criticalRatePercent;
            public float criticalDamagePercent;
            public float dodgePercent;
            public float damageReductionPercent;
            public float healingPercent;
            public float lootPercent;
            public float cooldownReductionPercent;
            public float manaRecoveryPercent;
        }

        private readonly Button[] _branchButtons = new Button[BranchCount];
        private readonly Image[] _branchImages = new Image[BranchCount];
        private readonly TMP_Text[] _branchNames = new TMP_Text[BranchCount];
        private readonly TMP_Text[] _branchProgress = new TMP_Text[BranchCount];
        private readonly Button[] _nodeButtons = new Button[NodeCount];
        private readonly Image[] _nodeImages = new Image[NodeCount];
        private readonly Image[] _nodeIcons = new Image[NodeCount];
        private readonly Image[] _nodeLocks = new Image[NodeCount];
        private readonly TMP_Text[] _nodeNames = new TMP_Text[NodesPerBranch];
        private ScrollRect _treeScroll;
        private Button _learnButton;
        private readonly Outline[] _nodeOutlines = new Outline[NodeCount];
        private readonly Image[] _connectorImages = new Image[ConnectorCount];

        private TMP_FontAsset _font;
        private Sprite _backgroundSprite;
        private Sprite _headerSprite;
        private Sprite _backButtonSprite;
        private Sprite _utilityButtonSprite;
        private Sprite _trigramIdleSprite;
        private Sprite _trigramActiveSprite;
        private Sprite _nodeFrameSprite;
        private Sprite _nodeActiveSprite;
        private Sprite _nodeReadySprite;
        private Sprite _nodeLockedSprite;
        private Sprite _connectorSprite;
        private Sprite _detailsPanelSprite;
        private readonly Sprite[] _talentIcons = new Sprite[8];
        private Sprite _swordIcon;
        private Sprite _runnerIcon;
        private Sprite _gustIcon;
        private Sprite _lockIcon;
        private Sprite _statDividerSprite;

        private GameObject _page;
        private GameObject _detailsCard;
        private GameObject _confirmDialog;
        private Image _detailsIconFrame;
        private Image _detailsIcon;
        private TMP_Text _pointText;
        private TMP_Text _detailsTitle;
        private TMP_Text _detailsEffect;
        private TMP_Text _detailsState;
        private TalentSave _save;
        private int _selectedBranch;
        private int _selectedTier;
        private int _lastClickBranch = -1;
        private int _lastClickTier = -1;
        private float _lastClickAt = -1f;

        public static event Action OnTalentsChanged;

        public static int CultivationRank =>
            Mathf.Clamp(PlayerPrefs.GetInt(RankKey, 6), 1, RealmNames.Length);

        public static int EarnedPoints => CultivationRank;
        public static int SpentPoints => CountLit(LoadSave());
        public static int AvailablePoints => Mathf.Max(0, EarnedPoints - SpentPoints);

        private void Awake()
        {
            _font = Resources.Load<TMP_FontAsset>("Fonts/LobbyDisplay") ?? TMP_Settings.defaultFontAsset;
            LoadArt();
            _save = LoadSave();
            BuildPage();
        }

        private void OnDisable() { Close(); }

public void Open()
{
    if (_page == null)
        BuildPage();

    _save = LoadSave();
    _confirmDialog.SetActive(false);
    _page.SetActive(true);
    Transform selectionPage = transform.Find(CharacterPanel.PageRootName);
    if (selectionPage != null) selectionPage.gameObject.SetActive(false);
    _page.transform.SetAsLastSibling();
    Refresh();
    ShowTalentDetails(_selectedBranch, _selectedTier);
}

        public static void SetCultivationRank(int rank)
        {
            PlayerPrefs.SetInt(RankKey, Mathf.Clamp(rank, 1, RealmNames.Length));
            PlayerPrefs.Save();
            OnTalentsChanged?.Invoke();
        }

        public static TalentBonuses GetBonuses()
        {
            TalentBonuses result = new();
            TalentSave save = LoadSave();

            for (int branch = 0; branch < BranchCount; branch++)
            for (int tier = 0; tier < NodesPerBranch; tier++)
            {
                if (save.lit[NodeIndex(branch, tier)])
                    AddEffect(ref result, branch, tier);
            }

            return result;
        }

private void LoadArt()
{
    _backgroundSprite = Q.Sprite("bg-paper");
    _nodeFrameSprite = _nodeActiveSprite = _nodeReadySprite = _nodeLockedSprite = (Q.Sprite("talent_node") ?? Q.Sprite("souljade_socket"));
    _trigramIdleSprite = _trigramActiveSprite = Q.Sprite("tab_underline");
    _detailsPanelSprite = Q.Sprite("info_card");
    string[] icons = { "sword", "jade_spiral", "shield", "flame", "speed", "attack_aoe", "healing", "talent" };
    for (int i = 0; i < icons.Length; i++) _talentIcons[i] = Q.Sprite(icons[i]);
    _swordIcon = Q.Sprite("sword");
    _runnerIcon = Q.Sprite("speed");
    _gustIcon = Q.Sprite("attack_aoe");
}

        private static Sprite LoadSprite(string name) => Resources.Load<Sprite>(ArtPath + name);

        private static Sprite LoadTextureAsSprite(string resourcesPath, Rect? uv = null)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
            Rect region = uv ?? new Rect(0, 0, 1, 1);
            return texture == null
                ? null
                : Sprite.Create(texture, new Rect(region.x * texture.width, region.y * texture.height, region.width * texture.width, region.height * texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
        }

private static Sprite LoadTalentNodeState(string resourcesPath, int stateIndex)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
            if (texture == null)
                return null;

            int cellWidth = texture.width / 3;
            return Sprite.Create(texture, new Rect(cellWidth * stateIndex, 0f, cellWidth, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
        }

private void BuildPage()
{
    if (_page != null) return;
    BuildQuietInkTalentPage();
}

// Keep the real 24-node tree inside the same portrait design area on phone and PC.
private void FitPortraitPage()
{
    var design = ImmortalV4PageKit.Rect(_page.transform, "PortraitDesign", 0, 0, 941, 1672);
    design.anchorMin = design.anchorMax = design.pivot = new Vector2(.5f,.5f);
    design.anchoredPosition = Vector2.zero;
    design.gameObject.AddComponent<LobbyPortraitLayout>();
    var children = new System.Collections.Generic.List<Transform>();
    foreach (Transform child in _page.transform)
        if (child != design && child.name != "CelestialMapBackground" && child != _confirmDialog.transform) children.Add(child);
    foreach (var child in children) child.SetParent(design,false);
    ImmortalV4PageKit.Art(design,"Talent/background",0,0,941,1672).transform.SetAsFirstSibling();
    var backdropShade = MakeImage(design, "TalentBackdropShade", null, Vector2.zero,
        new Vector2(941,1672), new Vector2(.5f,.5f), new Vector2(.5f,.5f), Image.Type.Simple, false);
    backdropShade.color = new Color(.012f,.035f,.065f,.54f);
    backdropShade.transform.SetSiblingIndex(1);
    var cover=ImmortalV4PageKit.Art(_page.transform,"Talent/background",0,0,941,1672);
    cover.rectTransform.anchorMin=cover.rectTransform.anchorMax=cover.rectTransform.pivot=new Vector2(.5f,.5f);cover.rectTransform.anchoredPosition=Vector2.zero;
    var fit=cover.gameObject.AddComponent<AspectRatioFitter>();fit.aspectRatio=941f/1672f;fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;cover.transform.SetAsFirstSibling();
    _page.transform.Find("CelestialMapBackground").gameObject.SetActive(false);
    design.Find("BackButton").gameObject.SetActive(false);
    ImmortalV4PageKit.Art(design,"Equipment/header_plate",0,15,455,130);
    ImmortalV4PageKit.Button(design,"ReferenceBack","",25,30,115,105,Close,null,62);
    var tree = (RectTransform)design.Find("SelectedSchoolTalentTree");
    // 24 个真实节点保留完整关系，在独立树区滚动，避免整棵缩小后无法辨认和点击。
    var viewport = ImmortalV4PageKit.Rect(design, "TalentTreeViewport", 20, 315, 901, 750);
    viewport.gameObject.AddComponent<RectMask2D>();
    var touchSurface = viewport.gameObject.AddComponent<Image>();
    touchSurface.color = new Color(.015f, .035f, .06f, .18f);
    touchSurface.raycastTarget = true;
    _treeScroll = viewport.gameObject.AddComponent<ScrollRect>();
    tree.SetParent(viewport, false);
    tree.anchorMin = tree.anchorMax = new Vector2(.5f, 1f);
    tree.pivot = new Vector2(.5f, 1f);
    tree.localScale = Vector3.one;
    tree.anchoredPosition = Vector2.zero;
    tree.sizeDelta = new Vector2(900, 950);
    _treeScroll.content = tree;
    _treeScroll.viewport = viewport;
    _treeScroll.horizontal = false;
    _treeScroll.vertical = true;
    _treeScroll.movementType = ScrollRect.MovementType.Clamped;
    _treeScroll.scrollSensitivity = 45f;
    for (int i=0;i<BranchCount;i++)
    {
        var tab = (RectTransform)design.Find("SchoolTab"+i);
        tab.localScale = Vector3.one;
        tab.sizeDelta = new Vector2(207, 84);
        tab.anchoredPosition = new Vector2(-321+i*214,-260);
        var icon = tab.Find("SchoolIcon") as RectTransform;
        icon.anchoredPosition = new Vector2(-65, 0);
        icon.sizeDelta = new Vector2(45, 45);
        _branchNames[i].rectTransform.anchoredPosition = new Vector2(26, 0);
        _branchNames[i].rectTransform.sizeDelta = new Vector2(116, 58);
        _branchNames[i].fontSize = 30;
    }
    var pointPlate = ImmortalV4PageKit.Art(design,"Lobby/currency_plate",60,163,821,62);
    pointPlate.transform.SetSiblingIndex(_pointText.transform.GetSiblingIndex());
    _pointText.rectTransform.anchoredPosition = new Vector2(0,-170);
    _pointText.fontSize = 28;
    foreach (var name in new[]{"PointRuleLeft","PointRuleRight","PointDiamondLeft","PointDiamondRight"})
        design.Find(name).gameObject.SetActive(false);
    var details = _detailsCard.GetComponent<RectTransform>();
    details.localScale = Vector3.one;
    details.sizeDelta = new Vector2(861, 335);
    details.anchoredPosition = new Vector2(0, 190);
    _detailsIconFrame.rectTransform.anchoredPosition = new Vector2(-300, 0);
    _detailsIconFrame.rectTransform.sizeDelta = new Vector2(200, 200);
    _detailsIcon.rectTransform.anchoredPosition = new Vector2(-300, 0);
    _detailsIcon.rectTransform.sizeDelta = new Vector2(132, 132);
    _detailsTitle.rectTransform.anchoredPosition = new Vector2(-170, 70);
    _detailsTitle.rectTransform.sizeDelta = new Vector2(520, 64);
    _detailsTitle.fontSize = 39;
    _detailsEffect.rectTransform.anchoredPosition = new Vector2(-170, 5);
    _detailsEffect.rectTransform.sizeDelta = new Vector2(520, 62);
    _detailsEffect.fontSize = 30;
    _detailsEffect.enableWordWrapping = true;
    _detailsState.rectTransform.anchoredPosition = new Vector2(-170, -65);
    _detailsState.rectTransform.sizeDelta = new Vector2(520, 64);
    _detailsState.fontSize = 24;
    _detailsState.enableWordWrapping = true;
    var bodyFont = Resources.Load<TMP_FontAsset>("Fonts/MSYaHei SDF");
    if (bodyFont != null)
    {
        _pointText.font = bodyFont;
        _detailsEffect.font = bodyFont;
        _detailsState.font = bodyFont;
        foreach (var label in _nodeNames) if (label != null) label.font = bodyFont;
    }
    details.Find("CloseDetails").gameObject.SetActive(false);
    var hintPlate = ImmortalV4PageKit.Art(design,"Lobby/currency_plate",90,1072,761,58);
    hintPlate.color = new Color(.65f,.78f,.87f,.94f);
    ImmortalV4PageKit.Text(design,"TreeScrollHint","上下滑动查看完整天赋 · 点击节点查看详情",120,1078,701,45,24);
    ImmortalV4PageKit.Text(design,"PageTitle","天赋",165,35,300,80,50);
    _learnButton = ImmortalV4PageKit.Button(design,"LearnSelected","修习",110,1515,350,105,()=>{UnlockSelected();Refresh();ShowTalentDetails(_selectedBranch,_selectedTier);},"Equipment/button_gold",40);
    ImmortalV4PageKit.Button(design,"ResetSelected","重置",480,1515,350,105,ShowResetDialog,"Equipment/button_jade",40);
    design.Find("ResetButton").gameObject.SetActive(false);
    _confirmDialog.transform.SetAsLastSibling();
}

private void BuildBranchSelectors(Transform parent)
{
    float[] columns = { -465f, -155f, 155f, 465f };
    int[] schoolIcons = { 0, 1, 2, 4 };
    for (int branch = 0; branch < BranchCount; branch++)
    {
        int captured = branch;
        Button selector = MakeButton(parent, "SchoolTab" + branch,
            new Vector2(columns[branch], -166f), new Vector2(270f, 88f),
            () => SelectBranch(captured), _trigramIdleSprite,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
        selector.GetComponent<Image>().preserveAspect = false;
        _branchButtons[branch] = selector;
        _branchImages[branch] = selector.GetComponent<Image>();

        Image icon = MakeImage(selector.transform, "SchoolIcon", _talentIcons[schoolIcons[branch]],
            new Vector2(-84f, 0f), new Vector2(64f, 64f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Image.Type.Simple, false);
        icon.preserveAspect = true;

        _branchNames[branch] = MakeText(selector.transform, "SchoolName", Branches[branch].name, 38,
            new Vector2(28f, 0f), new Vector2(156f, 60f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), MainText, TextAlignmentOptions.MidlineLeft, true, false);
        _branchProgress[branch] = null;
    }
}

private void BuildSelectedBranchTree(Transform parent)
{
    GameObject tree = new("SelectedSchoolTalentTree", typeof(RectTransform));
    tree.transform.SetParent(parent, false);
    RectTransform treeRect = tree.GetComponent<RectTransform>();
    treeRect.anchorMin = treeRect.anchorMax = new Vector2(0.5f, 0.5f);
    treeRect.pivot = new Vector2(0.5f, 0.5f);
    treeRect.anchoredPosition = new Vector2(0f, 18f);
    treeRect.sizeDelta = new Vector2(900f, 950f);
    Vector2[] positions =
    {
        new(0f, 365f),
        new(-285f, 185f), new(0f, 185f), new(285f, 185f),
        new(-365f, 5f), new(-220f, 5f), new(-73f, 5f),
        new(73f, 5f), new(220f, 5f), new(365f, 5f),
        new(-365f, -175f), new(-220f, -175f), new(-73f, -175f),
        new(73f, -175f), new(220f, -175f), new(365f, -175f),
        new(-385f, -355f), new(-275f, -355f), new(-165f, -355f), new(-55f, -355f),
        new(55f, -355f), new(165f, -355f), new(275f, -355f), new(385f, -355f)
    };
    float[] radii =
    {
        66f, 58f, 58f, 58f,
        53f, 53f, 53f, 53f, 53f, 53f,
        53f, 53f, 53f, 53f, 53f, 53f,
        48f, 48f, 48f, 48f, 48f, 48f, 48f, 48f
    };

    for (int node = 1; node < NodesPerBranch; node++)
        CreatePathSegment(tree.transform, node - 1, positions[Parents[node]], positions[node],
            radii[Parents[node]], radii[node]);

    for (int nodeIndex = 0; nodeIndex < NodesPerBranch; nodeIndex++)
    {
        float size = radii[nodeIndex] * 2f;
        Button node = MakeButton(tree.transform, "TalentNode" + nodeIndex, positions[nodeIndex],
            new Vector2(size, size), null, _nodeFrameSprite,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        Image nodeImage = node.GetComponent<Image>();
        nodeImage.preserveAspect = true;
        _nodeButtons[nodeIndex] = node;
        _nodeImages[nodeIndex] = nodeImage;

        Outline outline = node.gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(4f, -4f);
        outline.enabled = false;
        _nodeOutlines[nodeIndex] = outline;

        Image icon = MakeImage(node.transform, "TalentIcon", GetTalentIcon(nodeIndex, _selectedBranch),
            Vector2.zero, new Vector2(size * 0.66f, size * 0.66f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Image.Type.Simple, false);
        icon.preserveAspect = true;
        _nodeIcons[nodeIndex] = icon;

        Image lockImage = MakeImage(node.transform, "LockIcon", _lockIcon, Vector2.zero,
            new Vector2(46f, 46f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Image.Type.Simple, false);
        lockImage.preserveAspect = true;
        _nodeLocks[nodeIndex] = lockImage;
        _nodeNames[nodeIndex] = MakeText(node.transform, "TalentName", "", 23,
            new Vector2(0, -size * .5f - 22), new Vector2(nodeIndex < 16 ? 140 : 108, 38),
            new Vector2(.5f,.5f), new Vector2(.5f,.5f), MainText, TextAlignmentOptions.Center, false, false);
        _nodeNames[nodeIndex].raycastTarget = false;
        var nameShadow = _nodeNames[nodeIndex].gameObject.AddComponent<Shadow>();
        nameShadow.effectColor = new Color(.005f,.015f,.025f,.96f);
        nameShadow.effectDistance = new Vector2(1.5f,-2f);
        AddTalentPointerEvents(node.gameObject, nodeIndex);
    }
}

private void CreatePathSegment(Transform parent, int index, Vector2 fromCenter, Vector2 toCenter,
    float fromRadius, float toRadius)
{
    Vector2 direction = (toCenter - fromCenter).normalized;
    Vector2 start = fromCenter + direction * fromRadius;
    Vector2 end = toCenter - direction * toRadius;
    Vector2 delta = end - start;
    Image line = MakeImage(parent, "TalentPath" + index, null,
        (start + end) * 0.5f, new Vector2(delta.magnitude, 2.5f),
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Image.Type.Simple, false);
    line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    line.color = new Color(0.98f, 0.87f, 0.63f, 0.88f);
    _connectorImages[index] = line;
    line.transform.SetAsFirstSibling();
}

private void BuildDetailsCard(Transform parent)
{
    _detailsCard = new GameObject("TalentEffectDetails", typeof(RectTransform), typeof(Image));
    _detailsCard.transform.SetParent(parent, false);
    RectTransform rect = _detailsCard.GetComponent<RectTransform>();
    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
    rect.pivot = new Vector2(0.5f, 0f);
    rect.anchoredPosition = new Vector2(0f, 14f);
    rect.sizeDelta = new Vector2(1140f, 340f);

    Image panel = _detailsCard.GetComponent<Image>();
    panel.sprite = _detailsPanelSprite;
    panel.type = Image.Type.Simple;
    panel.preserveAspect = false;
    panel.color = Color.white;
    panel.raycastTarget = true;

    _detailsIconFrame = MakeImage(_detailsCard.transform, "SelectedIconFrame", _nodeReadySprite,
        new Vector2(-410f, 0f), new Vector2(162f, 162f), new Vector2(0.5f, 0.5f),
        new Vector2(0.5f, 0.5f), Image.Type.Simple, false);
    _detailsIconFrame.preserveAspect = true;
    _detailsIcon = MakeImage(_detailsCard.transform, "SelectedTalentIcon", GetTalentIcon(1, _selectedBranch),
        new Vector2(-410f, 0f), new Vector2(94f, 94f), new Vector2(0.5f, 0.5f),
        new Vector2(0.5f, 0.5f), Image.Type.Simple, false);
    _detailsIcon.preserveAspect = true;

    _detailsTitle = MakeText(_detailsCard.transform, "TalentName", "", 48,
        new Vector2(-288f, 48f), new Vector2(690f, 64f), new Vector2(0.5f, 0.5f),
        new Vector2(0f, 0.5f), Gold, TextAlignmentOptions.MidlineLeft, true, false);
    _detailsEffect = MakeText(_detailsCard.transform, "TalentEffect", "", 42,
        new Vector2(-288f, -8f), new Vector2(690f, 58f), new Vector2(0.5f, 0.5f),
        new Vector2(0f, 0.5f), MainText, TextAlignmentOptions.MidlineLeft, false, false);
    _detailsState = MakeText(_detailsCard.transform, "TalentState", "", 30,
        new Vector2(-288f, -61f), new Vector2(690f, 42f), new Vector2(0.5f, 0.5f),
        new Vector2(0f, 0.5f), PaleJade, TextAlignmentOptions.MidlineLeft, false, false);

    Button close = MakeButton(_detailsCard.transform, "CloseDetails", new Vector2(-60f, -47f),
        new Vector2(88f, 88f), () => _detailsCard.SetActive(false), null,
        new Vector2(1f, 1f), new Vector2(1f, 1f));
    TMP_Text closeText = MakeText(close.transform, "Label", "×", 42, Vector2.zero,
        new Vector2(46f, 46f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
        PaleJade, TextAlignmentOptions.Center, false, false);
    closeText.raycastTarget = false;
    _detailsCard.SetActive(false);
}
private void BuildResetButton(Transform parent)
{
    Button reset = MakeButton(parent, "ResetButton", new Vector2(-34f, -30f),
        new Vector2(270f, 96f), ShowResetDialog, _utilityButtonSprite,
        new Vector2(1f, 1f), new Vector2(1f, 1f));
    reset.GetComponent<Image>().type = Image.Type.Simple;
    reset.GetComponent<Image>().preserveAspect = true;
    MakeText(reset.transform, "Label", "重置", 42, Vector2.zero,
        new Vector2(228f, 70f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
        MainText, TextAlignmentOptions.Center, true, false);
}

        private void BuildResetDialog(Transform parent)
        {
            BuildQuietResetDialog();
        }

        private void AddTalentPointerEvents(GameObject target, int tier)
        {
            EventTrigger trigger = target.AddComponent<EventTrigger>();
            // EventTrigger 会截获拖动；转交给树区，确保从节点上也能正常滚动。
            foreach (var eventType in new[] { EventTriggerType.InitializePotentialDrag,
                EventTriggerType.BeginDrag, EventTriggerType.Drag, EventTriggerType.EndDrag, EventTriggerType.Scroll })
            {
                var dragType = eventType;
                var dragEntry = new EventTrigger.Entry { eventID = dragType };
                dragEntry.callback.AddListener(data =>
                {
                    if (_treeScroll == null || !(data is PointerEventData pointer)) return;
                    switch (dragType)
                    {
                        case EventTriggerType.InitializePotentialDrag: _treeScroll.OnInitializePotentialDrag(pointer); break;
                        case EventTriggerType.BeginDrag:
                            _lastClickBranch = _lastClickTier = -1;
                            _lastClickAt = -1f;
                            _treeScroll.OnBeginDrag(pointer);
                            break;
                        case EventTriggerType.Drag: _treeScroll.OnDrag(pointer); break;
                        case EventTriggerType.EndDrag: _treeScroll.OnEndDrag(pointer); break;
                        case EventTriggerType.Scroll: _treeScroll.OnScroll(pointer); break;
                    }
                });
                trigger.triggers.Add(dragEntry);
            }
            EventTrigger.Entry entry = new() { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(data =>
            {
                PointerEventData pointer = data as PointerEventData;
                int branch = _selectedBranch;
                bool rapidRepeat = branch == _lastClickBranch && tier == _lastClickTier
                    && Time.unscaledTime - _lastClickAt <= DoubleClickWindow;
                if ((pointer != null && pointer.clickCount >= 2) || rapidRepeat)
                {
                    _lastClickBranch = _lastClickTier = -1;
                    _lastClickAt = -1f;
                    OnTalentDoubleClick(branch, tier);
                    return;
                }

                _lastClickBranch = branch;
                _lastClickTier = tier;
                _lastClickAt = Time.unscaledTime;
                OnTalentSingleClick(branch, tier);
            });
            trigger.triggers.Add(entry);
        }

        private void OnTalentSingleClick(int branch, int tier)
        {
            _selectedBranch = branch;
            _selectedTier = tier;
            Refresh();
            ShowTalentDetails(branch, tier);
        }

        private void OnTalentDoubleClick(int branch, int tier)
        {
            _selectedBranch = branch;
            _selectedTier = tier;
            Refresh();
            UnlockSelected();
            ShowTalentDetails(branch, tier);
        }

private void ShowTalentDetails(int branchIndex, int tier)
{
    if (_detailsCard == null)
        return;

    Branch branch = Branches[branchIndex];
    bool isBaseNode = tier == 0;
    bool lit = isBaseNode || _save.lit[NodeIndex(branchIndex, tier)];
    bool hasPrerequisites = HasPrerequisites(_save, branchIndex, tier);

    _detailsTitle.text = branch.talents[tier];
    _detailsEffect.text = branch.effects[tier];
    _detailsState.text = isBaseNode ? "主脉核心 · 已开启"
        : lit ? "已点亮 · 本地配置已保存"
        : !hasPrerequisites ? "先点亮前置节点后可解锁"
        : AvailablePoints > 0 ? "双击节点点亮 · 消耗 1 点"
        : "当前没有可用天赋点";
    _detailsState.color = lit ? Q.Jade : hasPrerequisites ? Q.Blue : Q.Muted;

    _detailsIcon.sprite = GetTalentIcon(tier, branchIndex);
    _detailsIcon.color = Color.white;
    _detailsIconFrame.sprite = isBaseNode || lit ? _nodeActiveSprite
        : hasPrerequisites ? _nodeReadySprite : _nodeLockedSprite;
    _detailsIconFrame.color = Color.white;
    _detailsCard.SetActive(true);
    if (_learnButton != null) _learnButton.interactable = !isBaseNode && !lit && hasPrerequisites && AvailablePoints > 0;
    _detailsCard.transform.SetAsLastSibling();
}

private void SelectBranch(int branch)
{
    _selectedBranch = Mathf.Clamp(branch, 0, BranchCount - 1);
    _selectedTier = 1;
    _lastClickBranch = _lastClickTier = -1;
    _lastClickAt = -1f;
    if (_treeScroll != null) _treeScroll.verticalNormalizedPosition = 1f;
    Refresh();
    ShowTalentDetails(_selectedBranch, _selectedTier);
}

private void Refresh()
{
    if (_save == null) _save = LoadSave();
    int earned = EarnedPoints, spent = SpentPoints, available = Mathf.Max(0, earned - spent);
    _pointText.text = $"灵根等级 {earned}  ·  已用 {spent} 点  ·  可用 {available} 点";
    for (int branch = 0; branch < BranchCount; branch++)
    {
        bool selected = branch == _selectedBranch;
        _branchImages[branch].color = selected ? Color.white : Color.clear;
        _branchNames[branch].text = Branches[branch].name;
        _branchNames[branch].color = selected ? Q.Jade : Q.Muted;
    }
    for (int node = 0; node < NodesPerBranch; node++)
    {
        bool lit = node == 0 || _save.lit[NodeIndex(_selectedBranch, node)];
        bool ready = node == 0 || HasPrerequisites(_save, _selectedBranch, node);
        _nodeImages[node].sprite = (Q.Sprite("talent_node") ?? Q.Sprite("souljade_socket"));
        _nodeImages[node].color = lit ? Color.white : ready ? new Color(1, 1, 1, .88f) : new Color(.83f, .85f, .82f, .85f);
        _nodeIcons[node].sprite = GetTalentIcon(node, _selectedBranch);
        _nodeIcons[node].enabled = true;
        _nodeIcons[node].color = lit || ready ? Color.white : new Color(1, 1, 1, .4f);
        _nodeNames[node].text = Branches[_selectedBranch].talents[node];
        _nodeNames[node].color = lit ? Q.Jade : ready ? Q.Ink : Q.Muted;
        _nodeLocks[node].enabled = false;
        _nodeOutlines[node].enabled = node == _selectedTier;
        _nodeOutlines[node].effectColor = Q.Jade;
        _nodeOutlines[node].effectDistance = new Vector2(1.5f, -1.5f);
    }
    for (int child = 1; child < NodesPerBranch; child++)
    {
        bool litEdge = _save.lit[NodeIndex(_selectedBranch, child)];
        bool connected = Parents[child] == 0 || _save.lit[NodeIndex(_selectedBranch, Parents[child])];
        Color color = litEdge ? Q.Jade : connected ? Q.Blue : Q.LineColor;
        if (quietPaths[child - 1] != null)
            foreach (var line in quietPaths[child - 1]) line.color = color;
    }
}
private bool UnlockSelected()
{
    if (_selectedTier == 0)
        return false;

    int index = NodeIndex(_selectedBranch, _selectedTier);
    if (_save.lit[index] || !HasPrerequisites(_save, _selectedBranch, _selectedTier)
        || AvailablePoints <= 0)
        return false;

    _save.lit[index] = true;
    Save();
    return true;
}

private static bool HasPrerequisites(TalentSave save, int branch, int tier)
{
    int parent = Parents[tier];
    return parent < 0 || parent == 0 || save.lit[NodeIndex(branch, parent)];
}

private void ShowResetDialog()
{
    _confirmDialog.SetActive(true);
    _confirmDialog.transform.SetAsLastSibling();
}

private void ResetTalents()
{
    _save = new TalentSave();
    Save();
    _confirmDialog.SetActive(false);
    ShowTalentDetails(_selectedBranch, _selectedTier);
}

private void Save()
{
    _save.version = 3;
    PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_save));
    PlayerPrefs.Save();
    OnTalentsChanged?.Invoke();
    Refresh();
}

        private void Close()
        {
            _lastClickBranch = _lastClickTier = -1;
            _lastClickAt = -1f;
            if (_detailsCard != null) _detailsCard.SetActive(false);
            if (_confirmDialog != null) _confirmDialog.SetActive(false);
            if (_page != null) _page.SetActive(false);
            Transform selectionPage = transform.Find(CharacterPanel.PageRootName);
            if (selectionPage != null) selectionPage.gameObject.SetActive(true);
        }

private static TalentSave LoadSave()
{
    string json = PlayerPrefs.GetString(SaveKey, string.Empty);
    if (string.IsNullOrEmpty(json)) json = PlayerPrefs.GetString(PreviousSaveKey, string.Empty);
    if (string.IsNullOrEmpty(json)) json = PlayerPrefs.GetString(LegacySaveKey, string.Empty);
    if (string.IsNullOrEmpty(json)) return new TalentSave();

    TalentSave source = null;
    try { source = JsonUtility.FromJson<TalentSave>(json); }
    catch (Exception exception) { Debug.LogWarning($"[RoleTalentPanel] Invalid talent save: {exception.Message}"); }
    if (source?.lit == null) return new TalentSave();

    if (source.lit.Length == NodeCount)
    {
        for (int branch = 0; branch < BranchCount; branch++)
            source.lit[NodeIndex(branch, 0)] = false;
        source.version = 3;
        return source;
    }

    TalentSave migrated = new();
    if (source.lit.Length == 60)
    {
        for (int branch = 0; branch < BranchCount; branch++)
        for (int tier = 1; tier < 15; tier++)
        {
            if (!source.lit[branch * 15 + tier]) continue;
            migrated.lit[NodeIndex(branch, tier)] = true;
            for (int ancestor = Parents[tier]; ancestor > 0; ancestor = Parents[ancestor])
                migrated.lit[NodeIndex(branch, ancestor)] = true;
        }
    }
    else if (source.lit.Length == 32 || source.lit.Length == 24)
    {
        int oldNodesPerSchool = source.lit.Length / 8;
        int[] schoolMap = { 0, 1, 2, 0, 3, 1, 2, 3 };
        for (int oldSchool = 0; oldSchool < 8; oldSchool++)
        {
            int school = schoolMap[oldSchool];
            int route = oldSchool % 3;
            int[] routeNodes = route == 0 ? new[] { 1, 4, 10 } : route == 1 ? new[] { 2, 6, 12 } : new[] { 3, 8, 14 };
            int tiersToCopy = Mathf.Min(oldNodesPerSchool, 4);
            for (int oldTier = 1; oldTier < tiersToCopy; oldTier++)
            {
                if (!source.lit[oldSchool * oldNodesPerSchool + oldTier]) continue;
                int node = routeNodes[oldTier - 1];
                migrated.lit[NodeIndex(school, node)] = true;
                for (int ancestor = Parents[node]; ancestor > 0; ancestor = Parents[ancestor])
                    migrated.lit[NodeIndex(school, ancestor)] = true;
            }
        }
    }

    migrated.version = 3;
    PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(migrated));
    PlayerPrefs.Save();
    return migrated;
}
        private static int CountLit(TalentSave save)
        {
            int count = 0;
            if (save?.lit == null)
                return count;
            foreach (bool lit in save.lit)
                if (lit) count++;
            return count;
        }

        private static int CountBranch(TalentSave save, int branch)
        {
            int count = 0;
            for (int tier = 0; tier < NodesPerBranch; tier++)
                if (save.lit[NodeIndex(branch, tier)]) count++;
            return count;
        }

        private static int NodeIndex(int branch, int tier) => branch * NodesPerBranch + tier;

        private Sprite GetTalentIcon(int tier, int branch)
        {
            int icon = IconIndices[Mathf.Clamp(tier, 0, IconIndices.Length - 1)];
            if (_talentIcons[icon] != null) return _talentIcons[icon];
            return tier % 3 == 0 ? _swordIcon : tier % 3 == 1 ? _runnerIcon : _gustIcon;
        }

private static void AddEffect(ref TalentBonuses value, int branch, int tier)
{
    if (tier == 0)
        return;

    if (branch == 0 && (tier == 1 || tier == 4))
    {
        value.attackPercent += 4f;
        return;
    }

    switch (branch)
    {
        case 0:
            value.attackPercent += tier < 10 ? 2f : 3f;
            if (tier % 3 == 2) value.criticalRatePercent += 1f;
            if (tier % 5 == 4) value.criticalDamagePercent += 3f;
            break;
        case 1:
            value.skillDamagePercent += tier < 10 ? 2f : 3f;
            if (tier % 3 == 2) value.manaRecoveryPercent += 3f;
            if (tier % 4 == 3) value.cooldownReductionPercent += 1f;
            break;
        case 2:
            value.healthPercent += tier % 2 == 0 ? 2f : 1f;
            if (tier % 3 == 1) value.defensePercent += 2f;
            if (tier % 4 == 2) value.damageReductionPercent += 1f;
            break;
        case 3:
            value.attackSpeedPercent += tier % 2 == 0 ? 2f : 1f;
            if (tier % 3 == 1) value.dodgePercent += 2f;
            if (tier % 4 == 3) value.cooldownReductionPercent += 1f;
            break;
    }
}
        private static Button MakeButton(Transform parent, string name, Vector2 position,
            Vector2 dimensions, UnityEngine.Events.UnityAction action, Sprite sprite,
            Vector2 anchor, Vector2 pivot)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = sprite == null ? new Color(1f, 1f, 1f, 0f) : Color.white;
            image.preserveAspect = sprite != null;
            image.raycastTarget = true;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.84f, 0.94f, 0.88f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            if (action != null)
                button.onClick.AddListener(action);
            return button;
        }

        private Image MakeImage(Transform parent, string name, Sprite sprite, Vector2 position,
            Vector2 dimensions, Vector2 anchor, Vector2 pivot, Image.Type type, bool raycastTarget)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            image.color = Color.white;
            image.preserveAspect = type == Image.Type.Simple;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private TMP_Text MakeText(Transform parent, string name, string value, int size,
            Vector2 position, Vector2 dimensions, Vector2 anchor, Vector2 pivot,
            Color color, TextAlignmentOptions alignment, bool bold, bool wrap)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = _font != null ? _font : TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.extraPadding = true;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private readonly struct Branch
        {
            public readonly string trigram;
            public readonly string name;
            public readonly string description;
            public readonly Color accent;
            public readonly string[] talents;
            public readonly string[] effects;

            public Branch(string trigram, string name, string description, Color accent,
                string[] talents, string[] effects)
            {
                this.trigram = trigram;
                this.name = name;
                this.description = description;
                this.accent = accent;
                this.talents = talents;
                this.effects = effects;
            }
        }
    }
}
