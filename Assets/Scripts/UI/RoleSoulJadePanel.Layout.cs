using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    public sealed partial class RoleSoulJadePanel
    {
        private RectTransform design;
        private RectTransform operationDesign;
        private GameObject operationView;
        private readonly Dictionary<RectTransform, Vector2> defaultPositions = new Dictionary<RectTransform, Vector2>();
        private readonly Dictionary<RectTransform, Vector2> defaultSizes = new Dictionary<RectTransform, Vector2>();
        private readonly Dictionary<RectTransform, Vector3> defaultScales = new Dictionary<RectTransform, Vector3>();
        private readonly Dictionary<TMP_Text, float> defaultFontSizes = new Dictionary<TMP_Text, float>();
        private bool layoutWasWide;
        private bool operationIsUpgrade;
        private Vector2 lastWideSize;
        private int lastWideItemCount = -1;
        private readonly Dictionary<string, Sprite> layoutSprites = new Dictionary<string, Sprite>();

        // All artwork and hit targets share the reference image's 941 x 1672 space.
        private void LateUpdate()
        {
            if (design == null || page == null || !page.activeSelf) return;
            RectTransform pageRect = (RectTransform)page.transform;
            Canvas canvas = page.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 min = Vector2.zero, max = new Vector2(Screen.width, Screen.height);
            Rect safe = Screen.safeArea;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(pageRect, safe.min, camera, out Vector2 localMin) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(pageRect, safe.max, camera, out Vector2 localMax))
            { min = localMin; max = localMax; }
            Vector2 size = max - min;
            design.anchoredPosition = (min + max) * .5f;
            bool wide = false; // Preserve the portrait composition on PC.
            if (wide)
            {
                design.sizeDelta = size;
                design.localScale = Vector3.one;
                bool changed = !layoutWasWide || (size - lastWideSize).sqrMagnitude > 1f || lastWideItemCount != ownedJades.Count;
                if (changed)
                {
                    RestoreLayoutDefaults();
                    ApplyWideLayout(size);
                    layoutWasWide = true;
                    lastWideSize = size;
                    lastWideItemCount = ownedJades.Count;
                }
            }
            else
            {
                design.sizeDelta = new Vector2(941f, 1672f);
                design.localScale = Vector3.one * Mathf.Min(size.x / 941f, size.y / 1672f);
                if (layoutWasWide)
                {
                    RestoreLayoutDefaults();
                    layoutWasWide = false;
                }
            }

            if (operationDesign != null)
            {
                operationDesign.anchoredPosition = (min + max) * .5f;
                operationDesign.sizeDelta = wide ? size : new Vector2(941f, 1672f);
                operationDesign.localScale = wide ? Vector3.one : design.localScale;
            }

            ConfigureInventoryGrid();
        }

        private void ConfigureInventoryGrid()
        {
            if (inventoryGrid == null || inventoryScroll == null) return;
            const int columns = 5;
            const float gap = 14f;
            float width = inventoryScroll.viewport.rect.width;
            float cellSize = Mathf.Max(1f, (width - 16f - gap * (columns - 1)) / columns);
            inventoryGrid.constraintCount = columns;
            if (inventoryGrid.padding.left != 8 || inventoryGrid.padding.top != 4)
                inventoryGrid.padding = new RectOffset(8, 8, 4, 4);
            inventoryGrid.spacing = new Vector2(gap, gap);
            inventoryGrid.cellSize = new Vector2(cellSize, cellSize);
        }

        private void CaptureLayoutDefaults()
        {
            defaultPositions.Clear(); defaultSizes.Clear(); defaultScales.Clear(); defaultFontSizes.Clear();
            RememberLayout(design);
        }

        private void RememberLayout(Transform root)
        {
            if (root == null) return;
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect == design || rect == operationDesign || (operationView != null && rect == (RectTransform)operationView.transform)) continue;
                if (!defaultPositions.ContainsKey(rect)) defaultPositions.Add(rect, rect.anchoredPosition);
                if (!defaultSizes.ContainsKey(rect)) defaultSizes.Add(rect, rect.sizeDelta);
                if (!defaultScales.ContainsKey(rect)) defaultScales.Add(rect, rect.localScale);
            }
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
                if (!defaultFontSizes.ContainsKey(label)) defaultFontSizes.Add(label, label.fontSize);
        }

        private void RestoreLayoutDefaults()
        {
            foreach (var pair in defaultPositions) if (pair.Key != null) pair.Key.anchoredPosition = pair.Value;
            foreach (var pair in defaultSizes) if (pair.Key != null) pair.Key.sizeDelta = pair.Value;
            foreach (var pair in defaultScales) if (pair.Key != null) pair.Key.localScale = pair.Value;
            foreach (var pair in defaultFontSizes) if (pair.Key != null) pair.Key.fontSize = pair.Value;
        }

        private void ApplyWideLayout(Vector2 size)
        {
            float width = size.x, height = size.y;
            float margin = Mathf.Max(28f, width * .025f);
            SetTopRect("Back", margin, 54f, 88f, 88f);
            SetTopRect("Title", width * .105f, 86f, 300f, 150f);
            SetTopRect("RoleNamePlaque", width * .275f, 90f, 315f, 58f);
            SetTopRect("RoleName", width * .275f, 91f, 280f, 48f);
            SetTopRect("Motto", width * .275f, 145f, 390f, 38f);
            SetTopRect("EssenceFrame", width - margin - 150f, 42f, 270f, 54f);
            SetTopRect("EssenceAmount", width - margin - 150f, 42f, 220f, 42f);

            float ringSize = Mathf.Min(height * .64f, width * .48f);
            float ringX = width * .30f, ringY = height * .57f;
            SetTopRect("WanderingSwordsman", width * .16f, height * .54f, width * .40f, height * .92f);
            SetTopRect("AncientJadeMechanism", ringX, ringY, ringSize, ringSize);
            SetTopRect("FloatingJadeCore", ringX, ringY, ringSize * .40f, ringSize * .57f);
            float ringScale = ringSize / 630f;
            Vector2[] centres = { new Vector2(374,270), new Vector2(877,270), new Vector2(187,625),
                new Vector2(1065,625), new Vector2(374,969), new Vector2(877,969) };
            for (int i = 0; i < slotButtons.Length; i++)
            {
                RectTransform socket = slotButtons[i] != null ? slotButtons[i].GetComponent<RectTransform>() : null;
                if (socket != null) socket.sizeDelta = new Vector2(108f * ringScale,108f * ringScale);
                if (slotIcons[i] != null) slotIcons[i].rectTransform.sizeDelta = new Vector2(89f * ringScale,89f * ringScale);
                float sourceX = centres[i].x / 1254f;
                float sourceY = centres[i].y / 1254f;
                float x = ringX + (sourceX - .5f) * ringSize + (i % 2 == 0 ? -77f : 77f) * ringScale;
                float y = ringY + (sourceY - .5f) * ringSize;
                SetTopRect("SocketLabel_" + i, x, y, 44f * ringScale, 78f * ringScale);
                TMP_Text label = design.Find("SocketLabel_" + i)?.GetComponent<TMP_Text>();
                if (label != null) label.fontSize = Mathf.Clamp(26f * ringScale, 22f, 32f);
            }

            float panelWidth = Mathf.Clamp(width * .43f, width * .32f, Mathf.Min(900f,width * .49f));
            float detailX = width * .76f, detailY = height * .30f, panelHeight = height * .36f;
            float panelLeft = detailX - panelWidth * .5f, panelTop = detailY - panelHeight * .5f;
            SetTopRect("CurrentLoadoutTitlePlaque", detailX, height * .075f, panelWidth * .56f, 62f);
            SetTopRect("CurrentLoadoutTitle", detailX, height * .075f, panelWidth * .52f, 50f);
            SetTopRect("SelectedJadeDetailPlaque", detailX, detailY, panelWidth, panelHeight);
            SetTopRect("SelectedJadeIcon", panelLeft + panelWidth * .20f, detailY, 150f, 150f);
            SetTopRect("SelectedJadeName", panelLeft + panelWidth * .68f, panelTop + 62f, panelWidth * .58f, 48f);
            SetTopRect("SelectedJadeRarity", panelLeft + panelWidth * .68f, panelTop + 108f, panelWidth * .58f, 38f);
            SetTopRect("SelectedJadeAffixes", panelLeft + panelWidth * .68f, panelTop + panelHeight * .59f,
                panelWidth * .60f, 116f);
            float actionY = detailY + panelHeight * .5f - 36f;
            SetTopRect("EquipSelectedJade", panelLeft + panelWidth * .23f, actionY, panelWidth * .16f, 56f);
            SetTopRect("UpgradeSelectedJade", detailX, actionY, panelWidth * .16f, 56f);
            SetTopRect("RefineSelectedJade", panelLeft + panelWidth * .77f, actionY, panelWidth * .16f, 56f);

            float viewportWidth = Mathf.Min(width * .45f, panelWidth * 1.3f), viewportHeight = height * .43f;
            float inventoryX = width * .76f, inventoryY = height * .775f;
            SetTopRect("OwnedJadeCount", inventoryX, inventoryY - viewportHeight * .5f - 28f, viewportWidth, 38f);
            SetTopRect("OwnedJadeViewport", inventoryX, inventoryY, viewportWidth, viewportHeight);
            inventoryGrid.padding = new RectOffset(10,10,10,10);
            inventoryGrid.spacing = new Vector2(8f,8f);
            for (int i = 0; i < itemButtons.Count; i++)
            {
                RectTransform card = itemButtons[i].GetComponent<RectTransform>();
                RectTransform icon = itemIcons[i].rectTransform;
                int columns = Mathf.Clamp(Mathf.FloorToInt((viewportWidth + 8f) / 170f), 3, 6);
                float cellWidth = (viewportWidth - 24f - 8f * (columns - 1)) / columns;
                card.sizeDelta = new Vector2(cellWidth,cellWidth);
                icon.anchorMin = Vector2.zero;
                icon.anchorMax = Vector2.one;
                icon.offsetMin = new Vector2(8, 8);
                icon.offsetMax = new Vector2(-8, -8);
            }
            ApplyWideOperationLayout(size);
        }

        private void ApplyWideOperationLayout(Vector2 size)
        {
            if (operationView == null) return;
            float width = size.x, height = size.y;
            float margin = Mathf.Max(28f, width * .025f);
            SetOperationRect("OperationBack", margin, 42f, 64f, 64f);
            SetOperationRect("OperationTitlePlaque", width * .5f, height * .075f, 620f, 78f);
            SetOperationRect("OperationTitle", width * .5f, height * .073f, 540f, 52f);
            SetOperationRect("OperationSubtitle", width * .5f, height * .13f, 680f, 38f);

            float summaryX = width * .255f, panelY = height * .51f;
            float summaryWidth = Mathf.Clamp(width * .40f, width * .32f, width * .46f);
            float panelHeight = height * .62f;
            SetOperationRect("OperationSummaryPanel", summaryX, panelY, summaryWidth, panelHeight);
            float jadeIconSize = Mathf.Min(220f, height * .24f);
            SetOperationRect("OperationJadeIcon", summaryX, height * .39f, jadeIconSize, jadeIconSize);
            SetOperationRect("OperationJadeName", summaryX, height * .55f, summaryWidth * .82f, 48f);
            SetOperationRect("OperationJadeRarity", summaryX, height * .60f, summaryWidth * .82f, 38f);

            float detailX = width * .735f;
            float detailWidth = Mathf.Clamp(width * .43f, width * .32f, width * .49f);
            SetOperationRect("OperationDetailsPanel", detailX, height * .53f, detailWidth, height * .72f);
            SetOperationRect("OperationSectionHeading", detailX, height * .22f, detailWidth * .86f, 42f);
            SetOperationRect("UpgradeLevelValue", detailX, height * .30f, detailWidth * .86f, 50f);
            SetOperationRect("UpgradeDescription", detailX, height * .36f, detailWidth * .88f, 42f);
            SetOperationRect("OperationAffixHeading", detailX, height * .425f, detailWidth * .86f, 36f);
            SetOperationRect("RefineLevelValue", detailX, height * .27f, detailWidth * .86f, 44f);
            SetOperationRect("RefineDescription", detailX, height * .585f, detailWidth * .88f, 40f);

            float rowHeight = Mathf.Clamp(height * .055f, 30f, 38f);
            for (int i = 0; i < 4; i++)
            {
                SetOperationRect("OperationAffix_" + i, detailX, height * (.48f + i * .055f), detailWidth * .82f, rowHeight);
                SetOperationRect("RefineAffix_" + i, detailX, height * (.33f + i * .065f), detailWidth * .82f, rowHeight);
                SetOperationRect("EmptyAffix_" + i, detailX, height * (.33f + i * .065f), detailWidth * .82f, rowHeight);
            }

            bool hasSecondMaterial = !operationIsUpgrade;
            float materialY = height * (operationIsUpgrade ? .82f : .76f);
            float materialHeadingY = height * (operationIsUpgrade ? .71f : .645f);
            float materialHeight = Mathf.Clamp(height * (operationIsUpgrade ? .15f : .14f), 90f, 150f);
            float materialCardWidth = hasSecondMaterial ? detailWidth * .40f : detailWidth * .78f;
            SetOperationRect("OperationMaterialHeading", detailX, materialHeadingY, detailWidth * .86f, 36f);
            SetOperationRect("OperationMaterialPanel", detailX, materialY, detailWidth * .92f, materialHeight);
            float firstCardCenter = hasSecondMaterial ? detailX - detailWidth * .23f : detailX;
            float secondCardCenter = detailX + detailWidth * .23f;
            float tokenSize = Mathf.Clamp(height * .095f, 50f, 64f);
            float firstIconX = firstCardCenter - (hasSecondMaterial ? materialCardWidth * .31f : detailWidth * .28f);
            SetOperationRect("OperationMaterialIcon", firstIconX, materialY, tokenSize, tokenSize);
            SetOperationRect("OperationMaterialGlyph", firstIconX, materialY, tokenSize * .88f, tokenSize * .88f);
            SetOperationRect("OperationMaterialName", firstCardCenter + (hasSecondMaterial ? materialCardWidth * .15f : detailWidth * .12f), materialY - 8f, materialCardWidth * .75f, 40f);
            SetOperationRect("OperationMaterialHint", firstCardCenter + (hasSecondMaterial ? materialCardWidth * .15f : detailWidth * .12f), materialY + 27f, materialCardWidth * .75f, 30f);
            if (hasSecondMaterial)
            {
                SetOperationRect("RefineJadeMaterialIcon", secondCardCenter - materialCardWidth * .31f, materialY, tokenSize, tokenSize);
                SetOperationRect("RefineJadeMaterialName", secondCardCenter + materialCardWidth * .15f, materialY - 8f, materialCardWidth * .75f, 40f);
                SetOperationRect("RefineJadeMaterialHint", secondCardCenter + materialCardWidth * .15f, materialY + 27f, materialCardWidth * .75f, 30f);
            }

            float fontScale = Mathf.Clamp(height / 900f, .70f, 1.15f);
            foreach (TMP_Text label in operationView.GetComponentsInChildren<TMP_Text>(true))
                if (defaultFontSizes.TryGetValue(label, out float fontSize)) label.fontSize = fontSize * fontScale;
            float confirmHeight = Mathf.Clamp(height * .10f, 50f, 76f);
            float confirmY = height * (operationIsUpgrade ? .945f : .925f);
            SetOperationRect("OperationConfirmFrame", detailX, confirmY, detailWidth * .64f, confirmHeight);
            SetOperationRect("OperationConfirmDesign", detailX, confirmY, detailWidth * .60f, confirmHeight * .88f);
        }

        private void SetOperationRect(string name, float centerX, float centerY, float width, float height)
        {
            if (operationView == null) return;
            RectTransform rect = operationDesign != null ? operationDesign.Find(name) as RectTransform : null;
            if (rect == null) return;
            rect.anchoredPosition = new Vector2(centerX, -centerY);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void SetTopRect(string name, float centerX, float centerY, float width, float height)
        {
            if (design == null) return;
            RectTransform rect = design.Find(name) as RectTransform;
            if (rect == null) return;
            rect.anchoredPosition = new Vector2(centerX,-centerY);
            rect.sizeDelta = new Vector2(width,height);
        }
        private void Build()
        {
            if (page != null) return;
            // A lobby theme rebuild may destroy this component's old child hierarchy.
            // Rebuild UI handles without retaining destroyed inventory cards.
            itemButtons.Clear(); itemIcons.Clear(); itemSelectionOutlines.Clear();
            BuildQuietInkSoulJadePage();
            CaptureLayoutDefaults();
            RefreshSlots(); RefreshInventory(); RefreshDetail();
            LateUpdate();
            page.SetActive(false);
        }

        private void BuildHeader()
        {
            var back = A(design, "Back", "18_Back_Button_Medallion", 67, 57, 88, 88);
            Click(back, Close);
            T(design, "Title", "魂玉", 105, 275, 104, 300, 150, Gold);
            A(design, "RoleNamePlaque", "17_Title_Plaque", 269, 200, 310, 54);
            T(design, "RoleName", "清风道人", 31, 270, 200, 250, 44, Ivory);
            T(design, "Motto", "灵 魂 入 玉 · 以 战 养 战", 20, 281, 250, 370, 36, Muted);
            A(design, "EssenceFrame", "16_Resource_Counter_Frame", 783, 62, 258, 50);
            T(design, "EssenceAmount", "玉髓  1,280   ＋", 22, 799, 62, 210, 36, Ivory);
        }

        private void BuildJadeRing()
        {
            var ring = A(design, "AncientJadeMechanism", "03_Jade_Ring_Mechanism", 553, 505, 630, 630);
            A(design, "FloatingJadeCore", "04_Central_Jade_Core", 550, 505, 245, 354);
            // Centres measured in source texture coordinates, top-left origin.
            Vector2[] centres = { new Vector2(374,270), new Vector2(877,270),
                new Vector2(187,625), new Vector2(1065,625),
                new Vector2(374,969), new Vector2(877,969) };
            for (int i = 0; i < 6; i++)
            {
                int index = i;
                Vector2 anchor = new Vector2(centres[i].x / 1254f, 1f - centres[i].y / 1254f);
                var button = HitTarget(ring.transform, "Socket_" + i, anchor, Vector2.zero,
                    new Vector2(108, 108), () => SelectSlot(index));
                slotButtons[i] = button;
                slotIcons[i] = Art(button.transform, "EquippedJade_" + i, JadeIconAliases[i],
                    new Vector2(.5f,.5f), Vector2.zero, new Vector2(89,89), new Vector2(.5f,.5f));
                // Never outline individual alpha flecks in the source icon.
                slotOutlines[i] = slotIcons[i].gameObject.AddComponent<Outline>();
                slotOutlines[i].enabled = false;
            }
        }

        private void BuildDetail()
        {
            A(design,"SelectedJadeDetailPlaque","12_Item_Detail_Plaque",556,930,654,320,true);
            A(design,"CurrentLoadoutTitlePlaque","17_Title_Plaque",558,800,330,57);
            T(design,"CurrentLoadoutTitle","当前配装",28,558,800,290,44,Ivory);
            detailName=T(design,"SelectedJadeName","",32,594,853,245,42,Gold);
            detailRarity=T(design,"SelectedJadeRarity","",22,594,895,245,32,Gold);
            detailEffect=T(design,"SelectedJadeAffixes","",22,594,969,260,108,Ivory);
            detailEffect.alignment=TextAlignmentOptions.TopLeft;
            detailEffect.lineSpacing=-2;
            detailEffect.enableWordWrapping=false;
            detailLore=T(design,"SelectedJadeLore","",16,594,1012,244,30,Muted);
            detailLore.gameObject.SetActive(false);
            var equip=A(design,"EquipSelectedJade","17_Title_Plaque",635,1050,110,48,true);
            Click(equip,EquipSelected);
            equipLabel=T(equip.transform,"Label","装备",23,0,0,100,40,Ivory,true);
            var upgrade=A(design,"UpgradeSelectedJade","17_Title_Plaque",757,1050,110,48,true);
            upgradeButton=upgrade.GetComponent<Button>() ?? upgrade.gameObject.AddComponent<Button>();
            Bind(upgrade,ShowUpgradeView);
            upgradeLabel=T(upgrade.transform,"Label","升级",23,0,0,100,40,Ivory,true);
            var refine=A(design,"RefineSelectedJade","17_Title_Plaque",876,1050,110,48,true);
            refineButton=refine.GetComponent<Button>() ?? refine.gameObject.AddComponent<Button>();
            Bind(refine,ShowRefineView);
            refineLabel=T(refine.transform,"Label","洗练",23,0,0,100,40,Ivory,true);
        }

        private void BuildInventory()
        {
            ownedCountLabel=T(design,"OwnedJadeCount","已拥有 0 枚",25,470,1135,400,32,Muted);
            var viewportObject=new GameObject("OwnedJadeViewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));
            viewportObject.transform.SetParent(design,false);
            RectTransform viewportRect=(RectTransform)viewportObject.transform;
            viewportRect.anchorMin=viewportRect.anchorMax=new Vector2(0,1);
            viewportRect.pivot=new Vector2(.5f,.5f);
            viewportRect.anchoredPosition=new Vector2(470,-1400);
            viewportRect.sizeDelta=new Vector2(916,490);
            Image viewportImage=viewportObject.GetComponent<Image>();
            viewportImage.color=new Color(0,0,0,.001f);
            viewportImage.raycastTarget=true;
            var contentObject=new GameObject("OwnedJadeContent",typeof(RectTransform),typeof(GridLayoutGroup),typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportObject.transform,false);
            RectTransform content=(RectTransform)contentObject.transform;
            content.anchorMin=new Vector2(0,1); content.anchorMax=new Vector2(1,1);
            content.pivot=new Vector2(.5f,1); content.anchoredPosition=Vector2.zero;
            content.sizeDelta=Vector2.zero;
            inventoryGrid=contentObject.GetComponent<GridLayoutGroup>();
            inventoryGrid.startCorner=GridLayoutGroup.Corner.UpperLeft;
            inventoryGrid.startAxis=GridLayoutGroup.Axis.Horizontal;
            inventoryGrid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;
            inventoryGrid.constraintCount=5;
            inventoryGrid.spacing=new Vector2(14,14);
            inventoryGrid.padding=new RectOffset(8,8,4,4);
            inventoryGrid.cellSize=new Vector2(145,145);
            var fitter=contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            inventoryScroll=viewportObject.GetComponent<ScrollRect>();
            inventoryScroll.content=content;
            inventoryScroll.viewport=viewportRect;
            inventoryScroll.horizontal=false;
            inventoryScroll.vertical=true;
            inventoryScroll.movementType=ScrollRect.MovementType.Clamped;
            inventoryScroll.inertia=true;
            ConfigureInventoryGrid();
            for(int i=0;i<ownedJades.Count;i++) AddInventoryCard(i);
            ownedCountLabel.text="已拥有 "+ownedJades.Count+" 枚";
        }

        private void AddInventoryCard(int index)
        {
            if (inventoryScroll == null || index < 0 || index >= ownedJades.Count) return;
            var button = Q.Button(inventoryScroll.content, "OwnedJade_" + index, "", 0, 0, 145, 145,
                () => SelectLibraryJade(index), null);
            button.GetComponentInChildren<TMP_Text>().gameObject.SetActive(false);
            var surface = button.GetComponent<Image>();
            surface.color = new Color32(241, 238, 227, 230);
            itemButtons.Add(button);
            var outline = button.gameObject.AddComponent<Outline>();
            outline.enabled = false;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            itemSelectionOutlines.Add(outline);
            var icon = Q.Icon(button.transform, "jade_comma", 8, 8, 129, 129);
            icon.name = "JadeIcon";
            icon.rectTransform.anchorMin = Vector2.zero;
            icon.rectTransform.anchorMax = Vector2.one;
            icon.rectTransform.offsetMin = new Vector2(8, 8);
            icon.rectTransform.offsetMax = new Vector2(-8, -8);
            SetJadeIcon(icon, OwnedIcon(ownedJades[index]));
            itemIcons.Add(icon);
            RememberLayout(button.transform);
        }
        private void BuildOperationView(bool upgrade)
        {
            BuildQuietInkOperation(upgrade);
        }

        private RectTransform OperationPanel(Transform parent, string name,
            float centerX, float centerY, float width, float height, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(centerX, -centerY);
            rect.sizeDelta = new Vector2(width, height);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private void CloseOperationView()
        {
            if (operationView != null) operationView.SetActive(false);
        }
        private static void Bind(Image image,UnityAction action)
        {
            image.raycastTarget=true;
            Button button=image.GetComponent<Button>() ?? image.gameObject.AddComponent<Button>();
            button.targetGraphic=image;
            button.transition=Selectable.Transition.None;
            button.navigation=new Navigation{mode=Navigation.Mode.None};
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private Image A(Transform parent,string name,string asset,float x,float y,float w,float h,bool sliced=false,bool local=false)
        {
            var image=Art(parent,name,asset,local?new Vector2(.5f,.5f):new Vector2(0,1),
                new Vector2(x,-y),new Vector2(w,h),new Vector2(.5f,.5f));
            Rect crop=Rect.zero;
            if(asset=="12_Item_Detail_Plaque") crop=new Rect(46,140,2090,421);
            if(asset=="13_Inventory_Shelf") crop=new Rect(20,175,2130,395);
            if(asset=="15_Bottom_Navigation_Frame") crop=new Rect(29,188,2115,340);
            if(asset=="16_Resource_Counter_Frame") crop=new Rect(15,140,2144,405);
            if(asset=="17_Title_Plaque") crop=new Rect(48,172,2080,379);
            if(asset=="19_SoulJade_Display_Counter") crop=new Rect(6,220,2160,354);
            if(crop.width>0)
                image.sprite=Crop(asset+(sliced?"_Sliced":"_Trimmed"),asset,crop,2172,724,
                    sliced?new Vector4(200,100,200,100):Vector4.zero);
            if(sliced)
            {
                image.type=Image.Type.Sliced;
                image.preserveAspect=false;
                image.pixelsPerUnitMultiplier=5;
            }
            return image;
        }

        // Cropping the sprite's UV rectangle leaves original PNG files intact.
        private Sprite Crop(string key,string asset,Rect topLeft,float sourceWidth,float sourceHeight,Vector4 border)
        {
            if(layoutSprites.TryGetValue(key,out Sprite result)) return result;
            Sprite source=LoadIcon(asset);
            float sx=source.texture.width/sourceWidth, sy=source.texture.height/sourceHeight;
            Rect rect=new Rect(topLeft.x*sx,(sourceHeight-topLeft.y-topLeft.height)*sy,topLeft.width*sx,topLeft.height*sy);
            result=Sprite.Create(source.texture,rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,border*sx);
            result.name=key;
            layoutSprites.Add(key,result);
            return result;
        }

        private TMP_Text T(Transform parent,string name,string value,int fontSize,float x,float y,float w,float h,Color color,bool local=false)
        {
            return Text(parent,name,value,fontSize,new Vector2(x,-y),new Vector2(w,h),
                local?new Vector2(.5f,.5f):new Vector2(0,1),color,TextAlignmentOptions.Center,false,new Vector2(.5f,.5f));
        }

        private static void Click(Image image,UnityEngine.Events.UnityAction action)
        {
            image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>();
            button.targetGraphic=image;
            button.transition=Selectable.Transition.None;
            button.onClick.AddListener(action);
        }

        private void OnDestroy()
        {
            foreach(var sprite in layoutSprites.Values) if(sprite!=null) Destroy(sprite);
            layoutSprites.Clear();
        }
    }
}









