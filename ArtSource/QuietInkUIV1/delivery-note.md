仙途·清墨 UI V1 已完成效果图、资源导入及指定尺寸的 Unity 视觉检查。**正式游戏页面尚未替换。**

交付内容：

- 三张效果图覆盖 12 页：[核心页](Concepts/02-core-pages-v2.png)（登录、大厅、角色、战斗）、[养成页](Concepts/03-growth-pages.png)（装备、魂玉、天赋、结算）、[功能页](Concepts/04-service-pages.png)（选服、背包、任务、设置）。
- [Assets/](Assets/) 有 10 张最终 PNG：4 张背景、2 张人物、1 张标识及 3 张图集。Unity 副本在 `Assets/Art/Resources/UI/QuietInkV1/`，合计 **55 个 Sprite**；10 份副本与源图哈希一致。
- 登录分层预览为 `Assets/Prefabs/UI/QuietInkV1/LoginConcept.prefab`。Design 下有 8 个顶层图层，背景、人物、标识、控件与 TMP 文字独立，选服箭头已改为线段；Prefab 不含 AtlasReview。**按钮未接网络回调，不会登录、创建角色或修改玩家数据。**

根代理已用 Unity MCP 审核以下 1080×1920 截图，完整记录见 [unity-visual-review.json](unity-visual-review.json)：

- [登录分层合成](Concepts/05-login-unity-composite.png)：新清墨页面正确显示，Prefab 全层 Missing Script 为 0，C# 编译错误为 0。
- [导航图集](Concepts/06-navigation-unity-review.png)、[物品图集](Concepts/07-items-unity-review.png)、[控件图集](Concepts/08-controls-unity-review.png)：各 16 个 Sprite，在浅纸和深绿背景上未见明显彩边或串图。
- 九切片样例通过：206×68 按钮、206×125 面板、206×18 血条，以及登录页 501×82 选服行、601×118 主按钮均未见破角。

**深底上的深墨细节对比较低；正式主题为浅纸色，不能据此声明任意底色均适用。** 上述结果只覆盖这些截图和尺寸，不代表 12 个正式页面、全部交互、多屏适配或其他拉伸比例已完成验收。临时预览覆盖层已销毁，保留原游戏暂停状态。现有游戏 Console 的一条既存 Missing Script 记录不属于新预览 Prefab，不把本次检查写成全游戏 Console 零问题。

[asset-manifest.json](asset-manifest.json) 记录用途、尺寸、状态和截图；[atlas-layout.json](atlas-layout.json) 与 [item-atlas-layout.json](item-atlas-layout.json) 提供 48 个图集切片的实际 Rect。坐标从左上开始，Unity 使用 `y = texture.height - y - height`。历史图集文件的 generation_notes 与 review 字段保留，最新样例检查结果见 manifest，不应改写历史记录。

已导出 [QuietInkV1-project-assets.unitypackage](QuietInkV1-project-assets.unitypackage)（13,081,122 字节），归档可读取，含 57 个条目、12 个 pathname。该包用于**同一 g1Client 工程**，包含资源及登录预览；已有 TMP 字体、`LobbyPortraitLayout.cs`、`ArtTextureImportProcessor.cs` 以及 uGUI/TMP 包依赖不打包，不是可直接导入任意空工程的独立包。

[page-map.json](page-map.json) 保留真实功能边界：装备已有服务器穿戴/卸下，魂玉与天赋主要为本地功能，未接入的背包、任务奖励和排行继续保留空状态。效果图中的区服名与战斗摘要仅作构图示意，不能变成运行数据。

[Prompts/](Prompts/) 为基础提示词，[design-spec.json](design-spec.json) 为设计约束。[References/](References/) 仅供研究，[Candidates/](Candidates/) 为过程候选，二者均不是游戏成品；历史 01 版已由 02 版替代。效果图和审核截图只供对照，不可裁成扁平整页替代可交互界面。后续按现有逻辑逐页接入，再验证完整流程与多屏显示。

