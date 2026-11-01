using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XiuXian.Core
{
    /// <summary>
    /// 仙府入口与大厅「更多 → 洞府」共用同一套页面，避免两套展示不一致。
    /// </summary>
    public class XianFuPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button backButton;
        private LobbyFeaturePanel estatePage;

        private void OnEnable()
        {
            if (titleText != null)
            {
                titleText.gameObject.SetActive(false);
            }
            if (backButton != null)
            {
                backButton.gameObject.SetActive(false);
            }
            estatePage = GetComponent<LobbyFeaturePanel>() ?? gameObject.AddComponent<LobbyFeaturePanel>();
            estatePage.OpenWithClose(LobbyFeaturePanel.Feature.Estate, OnBackClicked);
        }

        private void OnBackClicked()
        {
            UIManager.Instance.Show(UIScreen.Main);
        }
    }
}
