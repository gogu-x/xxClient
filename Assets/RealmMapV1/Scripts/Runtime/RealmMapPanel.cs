using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using A = XiuXian.RealmMap.RealmMapArt;

namespace XiuXian.RealmMap
{
    /// <summary>
    /// Every entrance, status label and terrain layer is a separate UI object.
    /// No network requests or save writes. The host supplies availability and binds EntryRequested.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class RealmMapPanel : MonoBehaviour
    {
        public event Action<RealmMapNode> EntryRequested;
        public event Action Closed;
        public float Zoom => zoom;
        public int NodeCount => layout?.nodes?.Length ?? 0;
        public RealmMapNode SelectedNode { get; private set; }
        [NonSerialized] private bool callbacksBound;
        private RealmMapLayout layout;
        private RectTransform design, viewport, world;
        private GameObject details;
        private TMP_Text detailName, detailRegion, detailBody, detailState;
        private Button enterButton;
        private TMP_Text enterButtonLabel;
        private float zoom = 1, baseScale = 1, ignoreClickUntil;
        private readonly Dictionary<int, Vector2> pointers = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, PointerEventData> pointerEvents = new Dictionary<int, PointerEventData>();
        private readonly HashSet<int> suppressedPointers = new HashSet<int>();
        private readonly List<RectTransform> nameplates = new List<RectTransform>();
        private readonly Dictionary<string, EntryAvailability> entryAvailability = new Dictionary<string, EntryAvailability>();
        private readonly Dictionary<string, EntryVisual> entryVisuals = new Dictionary<string, EntryVisual>();
        private RealmMapEntranceMotion selectedMotion;

        private sealed class EntryAvailability
        {
            public bool available;
            public string detailMessage;
        }

        private sealed class EntryVisual
        {
            public RealmMapEntranceMotion motion;
            public Image statusBackground;
            public TMP_Text statusText;
        }

        private void Awake() { BuildForPreview(); }
        private void OnEnable() { if (!callbacksBound) BuildForPreview(); }
        private void OnDisable() { ClearPointers(); ResetEntranceMotion(); }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) { ClearPointers(); ResetEntranceMotion(); }
        }

        private void ClearPointers()
        {
            pointers.Clear(); pointerEvents.Clear(); suppressedPointers.Clear();
        }

        public static RealmMapPanel Show(Transform parent)
        {
            var root = A.Rect(parent, "RealmMapPreview", 0, 0, 0, 0);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            return root.gameObject.AddComponent<RealmMapPanel>();
        }

        public void Open()
        {
            gameObject.SetActive(true);
            if (design == null) BuildForPreview();
            foreach (var entry in entryVisuals) RefreshNodeStatus(entry.Key);
            if (SelectedNode != null) RefreshEntryState();
            transform.SetAsLastSibling();
        }

        public void Close() { gameObject.SetActive(false); Closed?.Invoke(); }

        /// <summary>Explicitly rebuilds composition; usable by the package's editor prefab builder.</summary>
        public void BuildForPreview()
        {
            callbacksBound = true;
            ResetEntranceMotion();
            var old = transform.Find("RealmMapDesign");
            if (old != null)
            {
                old.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
            }
            ClearPointers(); nameplates.Clear(); entryVisuals.Clear(); SelectedNode = null;
            layout = A.LoadLayout();
            design = A.Rect(transform, "RealmMapDesign", 0, 0, A.DesignWidth, A.DesignHeight);
            design.anchorMin = design.anchorMax = design.pivot = new Vector2(.5f, .5f);
            design.anchoredPosition = Vector2.zero;
            design.gameObject.AddComponent<RealmMapPortraitFit>();
            var pageBackground = A.Fill(design, "Paper", 0, 0, 941, 1672, A.Paper);
            pageBackground.raycastTarget = true;
            if (layout == null || layout.map_width <= 0 || layout.map_height <= 0 || A.Sprite("world-map-base") == null)
            {
                A.Text(design, "MissingResources", "秘境资源未导入", 100, 700, 741, 140, 38);
                return;
            }
            layout.initial_zoom = layout.initial_zoom > 0 ? layout.initial_zoom : 1.1f;
            layout.min_zoom = Mathf.Clamp(layout.min_zoom > 0 ? layout.min_zoom : .7f, .3f, 1.6f);
            layout.max_zoom = Mathf.Max(layout.min_zoom, Mathf.Min(1.6f, layout.max_zoom > 0 ? layout.max_zoom : 1.6f));
            viewport = A.Rect(design, "MapViewport", 0, 165, 941, 1507);
            var surface = viewport.gameObject.AddComponent<Image>(); surface.color = A.Paper;
            viewport.gameObject.AddComponent<RectMask2D>();
            AddGesture(viewport.gameObject);
            world = A.Rect(viewport, "WorldLayers", 0, 0, layout.map_width, layout.map_height);
            world.anchorMin = world.anchorMax = world.pivot = new Vector2(.5f, .5f);
            world.anchoredPosition = Vector2.zero;
            A.Art(world, "world-map-base", 0, 0, layout.map_width, layout.map_height, false);
            if (layout.nodes != null)
                foreach (var node in layout.nodes) BuildNode(node);
            A.Fill(design, "HeaderPaper", 0, 0, 941, 165, new Color(A.Paper.r, A.Paper.g, A.Paper.b, .96f));
            A.Button(design, "Back", "‹", 22, 32, 88, 88, Close, false, 52);
            A.Text(design, "Title", string.IsNullOrEmpty(layout.title) ? "山海秘境" : layout.title,
                185, 30, 571, 80, 48);
            A.Fill(design, "RegionRuleLeft", 320, 133, 70, 1.5f, A.Rule);
            A.Fill(design, "RegionRuleRight", 551, 133, 70, 1.5f, A.Rule);
            A.Text(design, "Region", layout.region, 385, 110, 171, 46, 27, A.Muted);
            ControlButton("ZoomIn", "+", 1425, () => SetZoom(zoom * 1.2f));
            ControlButton("ZoomOut", "−", 1520, () => SetZoom(zoom / 1.2f));
            var locate = ControlButton("Locate", "", 1330, ResetView);
            A.Art(locate.transform, "map_locate", 17, 14, 50, 50);
            A.Fill(design, "GestureCaptionPaper", 240, 1608, 461, 50, new Color(A.Paper.r, A.Paper.g, A.Paper.b, .8f));
            A.Text(design, "GestureCaption", "拖动探索 · 双指缩放", 240, 1608, 461, 50, 24, A.Muted);
            BuildDetails();
            ResetView();
        }

        private Button ControlButton(string name, string label, float y, UnityAction action)
        {
            var button = A.Button(design, name, label, 825, y, 84, 84, action, false, 42);
            var disc = A.Rect(button.transform, "ControlDisc", 0, 0, 84, 84).gameObject.AddComponent<RealmMapControlDisc>();
            disc.raycastTarget = false;
            disc.transform.SetAsFirstSibling();
            return button;
        }

        private void BuildNode(RealmMapNode node)
        {
            if (node == null || string.IsNullOrEmpty(node.art)) return;
            float x = layout.normalized ? node.x * layout.map_width : node.x;
            float y = layout.normalized ? node.y * layout.map_height : node.y;
            float w = Mathf.Max(32, node.width), h = Mathf.Max(32, node.height);
            var button = A.Button(world, "Entrance-" + node.id, "", x, y, w, h, () => SelectNode(node));
            var rect = (RectTransform)button.transform;
            rect.pivot = new Vector2(.5f, 0);
            // node x/y is the ground contact, independent of generated Sprite pivot.
            rect.anchoredPosition = new Vector2(x, -y);
            var art = A.Art(rect, node.art, 0, 0, w, h);
            // Preserve-aspect artwork must stay grounded when its aspect differs from the hit area.
            art.rectTransform.anchorMin = art.rectTransform.anchorMax = art.rectTransform.pivot = new Vector2(.5f, 0);
            art.rectTransform.anchoredPosition = Vector2.zero;
            var visual = new EntryVisual { motion = art.gameObject.AddComponent<RealmMapEntranceMotion>() };
            entryVisuals[node.id] = visual;
            AddGesture(rect.gameObject);
            if (node.show_label)
            {
                int count = Mathf.Clamp((node.name ?? "").Length, 1, 8);
                float plaqueH = Mathf.Max(120, count * 32 + 36);
                var plate = A.Art(rect, "nameplate_vertical", w - 5, h * .2f, 58, plaqueH, false);
                plate.name = "EntranceNameplate";
                nameplates.Add(plate.rectTransform);
                var text = A.Text(plate.transform, "EntranceName", VerticalName(node.name), 7, 15, 44, plaqueH - 30, 29);
                text.lineSpacing = 0;
            }
            var status = A.Fill(rect, "EntranceStatus", 0, 0, 106, 38, A.Paper);
            status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(.5f, 0);
            status.rectTransform.pivot = new Vector2(.5f, 1);
            status.rectTransform.anchoredPosition = new Vector2(0, -8);
            nameplates.Add(status.rectTransform);
            visual.statusBackground = status;
            visual.statusText = A.Text(status.transform, "StatusText", "", 3, 1, 100, 36, 24);
            RefreshNodeStatus(node.id);
        }

        private static string VerticalName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var chars = new string[value.Length];
            for (int i = 0; i < value.Length; i++) chars[i] = value[i].ToString();
            return string.Join("\n", chars);
        }

        private void BuildDetails()
        {
            var rect = A.Rect(design, "EntranceDetails", 34, 1260, 873, 385);
            details = rect.gameObject;
            var paper = details.AddComponent<Image>(); paper.color = new Color(A.Paper.r, A.Paper.g, A.Paper.b, .97f);
            var outline = details.AddComponent<Outline>(); outline.effectColor = A.Rule; outline.effectDistance = Vector2.one;
            detailName = A.Text(rect, "Name", "", 28, 20, 680, 64, 38, A.Ink, TextAlignmentOptions.Left);
            detailRegion = A.Text(rect, "Region", "", 30, 90, 700, 40, 24, A.Muted, TextAlignmentOptions.Left);
            detailBody = A.Text(rect, "Description", "", 30, 145, 810, 74, 26, A.Ink, TextAlignmentOptions.TopLeft);
            detailState = A.Text(rect, "EntryState", "", 30, 238, 450, 94, 26, A.Muted, TextAlignmentOptions.Left);
            A.Button(rect, "CloseDetails", "×", 780, 20, 65, 65,
                () => { details.SetActive(false); ResetEntranceMotion(); }, false, 36);
            enterButton = A.Button(rect, "EnterRealm", "进入秘境", 515, 260, 320, 88, RequestSelectedEntry, true, 31);
            enterButtonLabel = enterButton.GetComponentInChildren<TMP_Text>();
            details.SetActive(false);
        }

        public void SelectNode(RealmMapNode node)
        {
            if (node == null || Time.unscaledTime < ignoreClickUntil || details == null) return;
            ResetEntranceMotion();
            SelectedNode = node;
            if (entryVisuals.TryGetValue(node.id, out var visual))
            {
                selectedMotion = visual.motion;
                selectedMotion.Play(14 / Mathf.Max(.001f, baseScale * zoom));
            }
            detailName.text = node.name;
            detailRegion.text = string.IsNullOrEmpty(node.region) ? layout.region : node.region;
            detailBody.text = node.description ?? "";
            RefreshEntryState();
            details.SetActive(true);
        }

        private void RefreshEntryState()
        {
            bool available = SelectedNode != null && IsEntryAvailable(SelectedNode.id);
            bool canEnter = available && EntryRequested != null;
            EntryAvailability state = null;
            if (SelectedNode != null) entryAvailability.TryGetValue(SelectedNode.id, out state);
            if (detailState != null)
            {
                detailState.text = state != null && !string.IsNullOrEmpty(state.detailMessage)
                    ? state.detailMessage : available ? "该秘境已开放" : "该秘境尚未开放";
                detailState.color = available ? A.Jade : A.Muted;
            }
            if (enterButton != null) enterButton.interactable = canEnter;
            if (enterButtonLabel != null) enterButtonLabel.text = !available ? "尚未开放" : canEnter ? "进入秘境" : "暂不可进入";
        }

        /// <summary>Host-owned availability. Unspecified entrances remain closed.</summary>
        public void SetEntryAvailability(string id, bool available, string detailMessage)
        {
            if (string.IsNullOrEmpty(id)) return;
            entryAvailability[id] = new EntryAvailability { available = available, detailMessage = detailMessage };
            RefreshNodeStatus(id);
            if (SelectedNode != null && SelectedNode.id == id) RefreshEntryState();
        }

        public bool IsEntryAvailable(string id)
        {
            return !string.IsNullOrEmpty(id) && entryAvailability.TryGetValue(id, out var state) && state.available;
        }

        private void RefreshNodeStatus(string id)
        {
            if (!entryVisuals.TryGetValue(id, out var visual)) return;
            bool available = IsEntryAvailable(id);
            visual.statusText.text = available ? "开放" : "未开放";
            visual.statusText.color = available ? A.Jade : new Color32(108, 113, 107, 255);
            visual.statusBackground.color = available
                ? new Color32(229, 240, 226, 239) : new Color32(246, 242, 231, 218);
        }

        private void ResetEntranceMotion()
        {
            if (selectedMotion != null) selectedMotion.Stop();
            selectedMotion = null;
        }

        private void RequestSelectedEntry()
        {
            if (SelectedNode == null) return;
            if (IsEntryAvailable(SelectedNode.id) && EntryRequested != null) EntryRequested(SelectedNode);
            else RefreshEntryState();
        }

        private void AddGesture(GameObject target)
        {
            target.AddComponent<RealmMapGestureSurface>().owner = this;
        }

        public void ResetView()
        {
            if (world == null || viewport == null) return;
            baseScale = Mathf.Max(viewport.rect.width / layout.map_width, viewport.rect.height / layout.map_height);
            zoom = Mathf.Clamp(layout.initial_zoom, layout.min_zoom, layout.max_zoom);
            float scale = baseScale * zoom;
            world.localScale = Vector3.one * scale;
            world.anchoredPosition = new Vector2((.5f - layout.initial_center_x) * layout.map_width * scale,
                (layout.initial_center_y - .5f) * layout.map_height * scale);
            RefreshNameplateScale();
            ClampWorld();
        }

        public void SetZoom(float value) { ZoomAt(value, Vector2.zero); }
        private void ZoomAt(float value, Vector2 focus)
        {
            if (world == null) return;
            float previous = Mathf.Max(.001f, baseScale * zoom);
            zoom = Mathf.Clamp(value, layout.min_zoom, layout.max_zoom);
            float current = baseScale * zoom;
            Vector2 focusWorld = (focus - world.anchoredPosition) / previous;
            world.localScale = Vector3.one * current;
            world.anchoredPosition = focus - focusWorld * current;
            RefreshNameplateScale();
            ClampWorld();
        }

        private void RefreshNameplateScale()
        {
            // Keep names legible as the world zooms out; the portrait fit still handles device scale.
            float scale = Mathf.Max(1, 1 / Mathf.Max(.001f, baseScale * zoom));
            foreach (var plate in nameplates)
                if (plate != null) plate.localScale = Vector3.one * scale;
        }
        private void ClampWorld()
        {
            float x = Mathf.Max(0, (layout.map_width * baseScale * zoom - viewport.rect.width) * .5f);
            float y = Mathf.Max(0, (layout.map_height * baseScale * zoom - viewport.rect.height) * .5f);
            Vector2 p = world.anchoredPosition;
            world.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -x, x), Mathf.Clamp(p.y, -y, y));
        }

        private bool Local(PointerEventData e, out Vector2 point)
        {
            point = Vector2.zero;
            if (viewport == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, e.position, e.pressEventCamera, out point)) return false;
            // WorldLayers is anchored at the viewport center; the viewport itself has a top-left pivot.
            point -= viewport.rect.center;
            return true;
        }
        public void PointerDown(PointerEventData e)
        {
            if (!Local(e, out var point)) return;
            pointers[e.pointerId] = point;
            pointerEvents[e.pointerId] = e;
            if (pointers.Count > 1) SuppressGestureClicks();
        }
        public void PointerUp(PointerEventData e)
        {
            if (suppressedPointers.Remove(e.pointerId))
            {
                e.eligibleForClick = false;
                ignoreClickUntil = Time.unscaledTime + .15f;
            }
            pointers.Remove(e.pointerId);
            pointerEvents.Remove(e.pointerId);
        }
        private void SuppressGestureClicks()
        {
            foreach (var pointer in pointerEvents)
            {
                pointer.Value.eligibleForClick = false;
                suppressedPointers.Add(pointer.Key);
            }
            ignoreClickUntil = Time.unscaledTime + .15f;
        }
        public void BeginDrag(PointerEventData e)
        {
            if (!pointers.ContainsKey(e.pointerId)) PointerDown(e);
            SuppressGestureClicks();
        }
        public void Drag(PointerEventData e)
        {
            if (world == null || !Local(e, out var point)) return;
            e.eligibleForClick = false;
            if (!pointers.TryGetValue(e.pointerId, out var previous)) previous = point;
            Vector2 oldCenter; float oldDistance;
            Pair(out oldCenter, out oldDistance);
            pointers[e.pointerId] = point;
            if (pointers.Count == 2 && oldDistance > .01f)
            {
                Vector2 center; float distance;
                Pair(out center, out distance);
                ZoomAt(zoom * distance / oldDistance, oldCenter);
                world.anchoredPosition += center - oldCenter;
            }
            else world.anchoredPosition += point - previous;
            ignoreClickUntil = Time.unscaledTime + .15f;
            ClampWorld();
        }
        private void Pair(out Vector2 center, out float distance)
        {
            center = Vector2.zero; distance = 0;
            if (pointers.Count != 2) return;
            var iterator = pointers.Values.GetEnumerator();
            iterator.MoveNext(); var a = iterator.Current; iterator.MoveNext(); var b = iterator.Current;
            center = (a + b) * .5f; distance = Vector2.Distance(a, b);
        }
        public void Scroll(PointerEventData e)
        {
            if (Local(e, out var point)) ZoomAt(zoom * Mathf.Pow(1.15f, e.scrollDelta.y), point);
        }
    }
}
