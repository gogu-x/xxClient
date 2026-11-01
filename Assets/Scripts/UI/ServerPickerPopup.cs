using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    public class ServerPickerPopup : MonoBehaviour
    {
        [SerializeField] private Transform listContainer;
        [SerializeField] private ServerListItem itemPrefab;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backgroundMask;
        private readonly List<ServerInfo> servers = new List<ServerInfo>();
        private readonly List<ServerListItem> visibleItems = new List<ServerListItem>();
        private readonly List<ServerInfo> visibleServers = new List<ServerInfo>();
        private Action<ServerInfo> onSelected;
        private ServerInfo selected;
        private GameObject design;
        private Button confirmButton;
        private bool ownedOnly;
        private bool showRequested;

        private void Awake()
        {
            if (!showRequested) gameObject.SetActive(false);
        }

        public void SetServers(IEnumerable<ServerInfo> values)
        {
            int selectedId = selected != null ? selected.ServerId : 0;
            servers.Clear();
            if (values != null)
                foreach (var value in values)
                    if (value != null) servers.Add(value);
            selected = servers.Find(server => server.ServerId == selectedId);
            if (gameObject.activeSelf) Build();
        }

        public void Show(Action<ServerInfo> callback)
        {
            onSelected = callback;
            var current = UIManager.Instance != null ? UIManager.Instance.SelectedServer : null;
            selected = current == null ? null : servers.Find(server => server.ServerId == current.ServerId);
            ownedOnly = servers.Exists(server => server.Uid != 0);
            showRequested = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Build();
        }

        public void Hide()
        {
            showRequested = false;
            gameObject.SetActive(false);
        }

        private void Build()
        {
            foreach (Transform child in transform) child.gameObject.SetActive(false);
            if (design != null)
            {
                if (Application.isPlaying) Destroy(design);
                else DestroyImmediate(design);
            }
            visibleItems.Clear();
            visibleServers.Clear();
            var rect = transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            var mask = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            mask.sprite = null;
            mask.color = Q.Paper;
            mask.raycastTarget = true;
            var d = Q.Design(transform, "ServerPickerQuietInkV2");
            design = d.gameObject;
            Q.Art(d, "bg-paper", 0, 0, 941, 1672);
            closeButton = Q.Button(d, "Close", "‹", 22, 58, 90, 100, Hide, null, 52);
            Q.Text(d, "Title", "选择服务器", 150, 70, 641, 108, 54);
            Q.Art(d, "divider", 334, 181, 273, 15, true);
            Q.Divider(d, 42, 308, 857);
            Q.Tab(d, "Owned", "我的角色", 74, 232, 370, ownedOnly, () => SetFilter(true));
            Q.Tab(d, "All", "全部区服", 496, 232, 370, !ownedOnly, () => SetFilter(false));

            int count = servers.FindAll(server => !ownedOnly || server.Uid != 0).Count;
            var content = Q.Scroll(d, "Servers", 42, 355, 857, 834, count * 274);
            listContainer = content;
            foreach (var server in servers)
            {
                if (ownedOnly && server.Uid == 0) continue;
                var item = ServerListItem.Create(content, "Server_" + server.ServerId, visibleItems.Count * 274);
                item.Setup(server, SelectServer);
                item.SetSelected(selected != null && selected.ServerId == server.ServerId);
                visibleItems.Add(item);
                visibleServers.Add(server);
            }
            if (visibleItems.Count == 0)
                Q.Text(content, "Empty", ownedOnly ? "暂无已创建角色" : "暂无可用服务器",
                    80, 185, 697, 100, 34, Q.Muted);

            confirmButton = Q.Button(d, "Confirm", "确认选择", 170, 1250, 601, 118,
                ConfirmSelection, "button_primary", 44);
            confirmButton.interactable = selected != null && selected.IsOnline;
        }

        private void SetFilter(bool owned)
        {
            if (ownedOnly == owned) return;
            ownedOnly = owned;
            Build();
        }

        private void SelectServer(ServerInfo value)
        {
            if (value == null || !value.IsOnline) return;
            selected = value;
            for (int i = 0; i < visibleItems.Count; i++)
                visibleItems[i].SetSelected(visibleServers[i].ServerId == value.ServerId);
            if (confirmButton != null) confirmButton.interactable = true;
        }

        private void ConfirmSelection()
        {
            if (selected == null || !selected.IsOnline) return;
            var value = selected;
            Hide();
            onSelected?.Invoke(value);
        }
    }
}
