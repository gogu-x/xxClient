using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    /// <summary>Reusable server card; all names and availability come from ServerInfo.</summary>
    public class ServerListItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button selectButton;
        private ServerInfo _server;
        private Action<ServerInfo> _onSelected;
        private Outline _selectedOutline;
        private TMP_Text _stateDot;

        public static ServerListItem Create(Transform parent, string name, float y, float width = 857)
        {
            var card = Q.Panel(parent, name, 0, y, width, 254, "info_card");
            card.raycastTarget = true;
            var item = card.gameObject.AddComponent<ServerListItem>();
            item.selectButton = card.gameObject.AddComponent<Button>();
            item.selectButton.targetGraphic = card;
            item.selectButton.transition = Selectable.Transition.None;
            var artClip = Q.Rect(card.transform, "LandscapeClip", width - 350, 10, 338, 234);
            artClip.gameObject.AddComponent<RectMask2D>();
            var landscape = Q.RawArt(artClip, "bg-login", 0, 0, 338, 234);
            landscape.uvRect = new Rect(.38f, .37f, .60f, .32f);
            landscape.color = new Color(1, 1, 1, .58f);
            item.nameText = Q.Text(card.transform, "ServerName", "", 52, 50, width - 108, 76, 44,
                Q.Ink, TextAlignmentOptions.Left);
            item.nameText.enableWordWrapping = false;
            item.statusText = Q.Text(card.transform, "ServerState", "", 88, 147, width - 180, 48, 28,
                Q.Muted, TextAlignmentOptions.Left);
            item._stateDot = Q.Text(card.transform, "StateDot", "●", 52, 148, 25, 46, 21, Q.Jade);
            item._selectedOutline = card.gameObject.AddComponent<Outline>();
            item._selectedOutline.effectColor = new Color(Q.Jade.r, Q.Jade.g, Q.Jade.b, .72f);
            item._selectedOutline.effectDistance = new Vector2(1.5f, -1.5f);
            item._selectedOutline.useGraphicAlpha = true;
            item._selectedOutline.enabled = false;
            return item;
        }

        public void Setup(ServerInfo server, Action<ServerInfo> onSelected)
        {
            _server = server;
            _onSelected = onSelected;
            if (nameText != null) nameText.text = server?.Name ?? "";
            if (selectButton != null)
            {
                selectButton.interactable = server != null && server.IsOnline;
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(() => _onSelected?.Invoke(_server));
            }
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (_selectedOutline != null) _selectedOutline.enabled = selected;
            if (_server == null) return;
            if (statusText != null)
            {
                statusText.text = !_server.IsOnline ? "维护中" : selected ? "当前选择" :
                    _server.Uid != 0 ? "已有角色" : "可创建角色";
                statusText.color = selected ? Q.Jade : Q.Muted;
            }
            if (_stateDot != null) _stateDot.color = _server.IsOnline ? Q.Jade : Q.Cinnabar;
        }
    }
}
