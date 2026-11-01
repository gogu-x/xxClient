RealmMapV1 Unity integration
===========================

The runtime source here is synchronized with the installed Assets/RealmMapV1 version.
In g1Client, GameNavigation and RealmMapScreen connect this map to the game UI.
Only the lobby retains the six bottom tabs: 角色 / 修炼 / 秘境 / 宗门 / 背包 / 魂玉.
The map and other secondary pages use a back button, with no bottom navigation.
The lobby's large realm-entry button is removed.
These game host classes live under Assets/Scripts; they are not part of this portable package.
The host opens qingyun (青云古境) as the entrance to the existing current-trial battle flow.
The server still chooses the player's current tower layer. The other 23 entrances remain
closed; no separate backend realm progression, rewards or 24-level mapping is invented.

Requirements
- Unity 6000.5 compatible UGUI and TextMeshPro packages (already present in g1Client).
- Chinese TMP fonts. Body text uses Resources/Fonts/MSYaHei SDF, with the TMP default
  as fallback. Titles and map names prefer Resources/Fonts/LobbyDisplay and fall back to
  the body font. In another project, supply fonts with Chinese glyphs.

Installation, when desired
1. In PowerShell run from this folder:
   ./Install-Unity.ps1 -ProjectPath D:/gosrc/gotree/g1Client -WhatIf
   Review the destinations, then omit -WhatIf to copy the package.
   Existing differing files within Assets/RealmMapV1 require the explicit -Overwrite switch.
2. Let Unity import/compile the added scripts.
3. Choose 仙途 / 秘境资源包 / 导入资源并生成组合预制体.
   This verifies raw PNG hashes, dimensions, measured Sprite rectangles, pivots and borders;
   copies world/lobby JSON layouts; and builds two independently layered prefabs.
4. Prefabs appear under Assets/RealmMapV1/Prefabs:
   LobbyComposition.prefab is a static composition, with each image/text individually editable.
   RealmMapPreview.prefab is a standalone map preview. Drop it into a preview scene that has
   an EventSystem and a UI input module. Mouse drag/wheel, +/- buttons and two-pointer pinch
   work through EventSystem callbacks; no direct legacy Input API dependency is required.

Optional public API
- XiuXian.RealmMap.Editor.RealmMapArtImporter.Import() returns the real import report.
- XiuXian.RealmMap.Editor.RealmMapArtImporter.BuildPreviewPrefabs() rebuilds compositions.
- XiuXian.RealmMap.RealmMapPanel.Show(canvasTransform), Open(), Close(), ResetView(), SetZoom().
- EntryRequested(RealmMapNode) is an optional host callback. Unbound maps never send
  battle requests or change game data. NavigationRequested has been removed.
- SetEntryAvailability(string id, bool available, string detailMessage) updates each gate's
  status and details. IsEntryAvailable(string id) queries it; unspecified IDs default closed.
  Entry requires both an open ID and a bound EntryRequested callback.
- All 24 entrances have quiet status badges. Names, badges, art and terrain are independent.
- There is no selection halo. RealmMapEntranceMotion moves only the clicked artwork in a
  single .65-second rise/return with at most 1.8% scale change. Hit areas and plaques stay
  fixed; changing selection, closing or losing focus restores the resting pose.
- Pointer coordinates use the viewport center for stable wheel/pinch focus. Dragging or
  a two-pointer gesture suppresses entrance clicks through release, including a stationary
  second finger. Map labels retain their minimum design size while the terrain zooms out.
- RealmMapControlDisc draws the quiet round control backgrounds without extra PNG assets.

Layout contract
world-layout.json: map_width/map_height define the design coordinate domain, not bitmap
resolution. x/y are measured from the top-left and identify the node's ground contact.
If normalized=true, x/y are fractions of width/height. Entrance rect pivot is bottom-center.
Only show_label nodes display a persistent vertical plaque; all names appear on selection.
initial_center_x/y are normalized map coordinates. max_zoom is capped at 1.6 for the current
source quality. A 3840x2560 design domain DOES NOT mean a 3840x2560 image or a 4K texture.
The importer retains the actual source dimensions and refuses silent resize/upscale.

lobby-layout.json: ordered layers plus separate texts. Each layer contains asset, x/y,
width/height, preserveAspect and opacity. Text colors use #RRGGBB or #RRGGBBAA; alignment
accepts Left/Center/Right. The supplied layout is a reusable composition, not account data.

Validation boundary
Offline C# compilation and static metadata checks can be performed without opening Unity.
They do not establish Unity import, prefab creation, mouse/touch behavior, or visual parity.
The project import report and runtime screenshots are retained in the package root and
Verification directory. Packaging does not rerun those checks. Review the actual running
game and input results separately; this document does not claim pixel-exact 1:1 parity.
