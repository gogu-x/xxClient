using UnityEngine;
using UnityEngine.UI;

namespace XiuXian.Core
{
    /// <summary>
    /// 大厅交互入口：角色、修炼、秘境、宗门、背包、魂玉。
    /// LobbyReferenceView 负责生成画面，本组件只连接现有功能。
    /// </summary>
    public class MainPanel : MonoBehaviour
    {
        [Header("底部导航")]
        [SerializeField] private Button skillButton;
        [SerializeField] private Button battleButton;
        [SerializeField] private Button secretButton;
        [SerializeField] private Button inventoryButton;
        [SerializeField] private Button sectButton;
        [SerializeField] private Button magicArtifactButton;
        [SerializeField] private Button moreButton;

        [Header("快捷入口")]
        [SerializeField] private Button characterSideButton;
        [SerializeField] private Button inventorySideButton;
        [SerializeField] private Button taskSideButton;
        [SerializeField] private Button sectSideButton;
        [SerializeField] private Button activitySideButton;
        [SerializeField] private Button welfareSideButton;
        [SerializeField] private Button questGuideButton;
        [SerializeField] private Button questBannerButton;
        [SerializeField] private Button secretPortalButton;
        
        private LobbyFeaturePanel featurePanel;
private RoleSoulJadePanel soulJadePanel;

private void OnEnable()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.GetComponent<MobileAdaptiveCanvasScaler>() == null)
            {
                canvas.gameObject.AddComponent<MobileAdaptiveCanvasScaler>();
            }
            EnsureReferenceLobby();
            var lobbyRoot = (RectTransform)transform.Find(LobbyReferenceView.MarkerName);
            LobbyReferenceView.RefreshCharacterSelection(lobbyRoot);
            var battleBubble = GetComponent<LobbyBattleBubble>();
            if (battleBubble == null) battleBubble = gameObject.AddComponent<LobbyBattleBubble>();
            battleBubble.Attach(lobbyRoot);

            BindButton(lobbyRoot?.Find("AvatarGroup")?.GetComponent<Button>(), OnCharacterClicked);
            BindButton(skillButton, OnSkillClicked);
            BindButton(battleButton, OnAutoBattleClicked);
            BindButton(secretButton, OnSecretClicked);
            BindButton(inventoryButton, OnInventoryClicked);
            BindButton(sectButton, OnSectClicked);
            magicArtifactButton = LobbyReferenceView.EnsureSoulJadeShortcut(lobbyRoot) ?? magicArtifactButton;
            BindButton(magicArtifactButton, OnSoulJadeClicked);
            BindButton(moreButton, OnMoreClicked);

            BindButton(characterSideButton, OnCharacterClicked);
            BindButton(inventorySideButton, OnInventoryClicked);
            BindButton(taskSideButton, OnTaskClicked);
            BindButton(sectSideButton, OnSectClicked);
            BindButton(activitySideButton, OnActivityClicked);
            BindButton(welfareSideButton, OnWelfareClicked);
            BindButton(questGuideButton, OnTaskClicked);
            BindButton(questBannerButton, OnSecretClicked);
            BindButton(secretPortalButton, OnSecretClicked);

            soulJadePanel = GetComponent<RoleSoulJadePanel>() ?? gameObject.AddComponent<RoleSoulJadePanel>();
            featurePanel = GetComponent<LobbyFeaturePanel>() ?? gameObject.AddComponent<LobbyFeaturePanel>();
        }

        private void EnsureReferenceLobby()
        {
            if (transform.Find(LobbyReferenceView.MarkerName) != null)
            {
                ApplyBindings(LobbyReferenceView.GetBindings((RectTransform)transform.Find(LobbyReferenceView.MarkerName)));
                return;
            }

            ApplyBindings(LobbyReferenceView.Build((RectTransform)transform));
        }

private void ApplyBindings(LobbyReferenceView.Bindings bindings)
        {
            skillButton = bindings.Cultivation;
            battleButton = bindings.Battle;
            secretButton = bindings.Secret;
            inventoryButton = bindings.Inventory;
            sectButton = bindings.Sect;
            magicArtifactButton = bindings.MagicArtifact;
            moreButton = bindings.More;
            characterSideButton = bindings.CharacterSide;
            inventorySideButton = bindings.InventorySide;
            taskSideButton = bindings.TaskSide;
            sectSideButton = bindings.SectSide;
            activitySideButton = bindings.ActivitySide;
            welfareSideButton = bindings.WelfareSide;
            questGuideButton = bindings.QuestGuide;
            questBannerButton = bindings.QuestBanner;
            secretPortalButton = bindings.SecretPortal;
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static void OnCharacterClicked() => UIManager.Instance.Show(UIScreen.Character);

private void OnSkillClicked()
        {
            featurePanel?.Open(LobbyFeaturePanel.Feature.Cultivation);
        }

        private void OnAutoBattleClicked()
        {
            BattleView.Show(transform.root);
        }

private void OnSectClicked()
        {
            featurePanel?.Open(LobbyFeaturePanel.Feature.Sect);
        }

        private static void OnPartnerClicked()
        {
            Debug.Log("[MainPanel] 打开伙伴面板（占位）");
        }

        private static void OnRankClicked()
        {
            Debug.Log("[MainPanel] 打开排行面板（占位）");
        }

        private void OnSoulJadeClicked()
        {
            soulJadePanel?.Open();
        }

private void OnTaskClicked()
        {
            featurePanel?.Open(LobbyFeaturePanel.Feature.Task);
        }
    

private void OnSecretClicked()
        {
            RealmMapScreen.Show(transform);
        }

private void OnInventoryClicked()
        {
            featurePanel?.Open(LobbyFeaturePanel.Feature.Inventory);
        }

private void OnMoreClicked()
        {
            featurePanel?.Open(LobbyFeaturePanel.Feature.More);
        }

private void OnActivityClicked()
        {
            featurePanel?.Open(LobbyFeaturePanel.Feature.Activity);
        }

private void OnWelfareClicked()
        {
            featurePanel?.Open(LobbyFeaturePanel.Feature.Welfare);
        }
}
}


