# Phase 7-C2 · 首启引导 Canvas 本机验收手册

> 沙箱侧已落地纯 C# `OnboardingFlow`（流转/持久化，EditMode `OnboardingFlowTests` 全绿）。
> 本文件给**本机 Unity** 补全真实 Canvas 视图的布局、文案、动画与验收口径。
> 逻辑流转不依赖 Canvas；Canvas 只负责"展示 + 按钮接线"。

## 文件
- 已实现（沙箱）：`Assets/Scripts/Runtime/Onboarding/OnboardingFlow.cs`（IOnboardingView / IOnboardingStore / LogOnboardingView / PlayerPrefsOnboardingStore）
- 已实现（沙箱）：`Assets/Scripts/Runtime/ArtBinding/OnboardingUIRuntimeBridge.cs`（订阅 EVT_FIRST_LAUNCH → 转交 flow；暴露 AdvanceStep/SkipAll）
- **待本机补全**：`Assets/Scripts/Runtime/Onboarding/OnboardingCanvasView.cs`（可编译骨架，Inspector 绑定 + 动画）

## Canvas 结构（建议）
```
UIRoot
└─ OnboardingCanvas (GameObject, 挂 OnboardingCanvasView)
   ├─ Panel_Reveal    (GameObject + Text title/hint)   ← stepPanels[0]
   ├─ Panel_Assemble  (GameObject + Text title/hint)   ← stepPanels[1]
   ├─ Panel_Choose    (GameObject + Text title/hint)   ← stepPanels[2]
   ├─ Panel_Archive   (GameObject + Text title/hint)   ← stepPanels[3]
   ├─ Btn_Next  (Button)   → bridge.AdvanceStep()
   └─ Btn_Skip  (Button)   → bridge.SkipAll()
```
- `OnboardingCanvasView` 与 `OnboardingUIRuntimeBridge` 挂在**同一场景**（前者在 UIRoot，后者亦建议挂 UIRoot）。
- `OnboardingCanvasView.Awake` 会自动 `FindObjectOfType<OnboardingUIRuntimeBridge>()`；也可在 Inspector 手动拖拽 `bridge` 引用。

## 文案（取自 OnboardingHints，工程占位，design 后续可覆盖）
| 步骤 | title | hint |
|------|-------|------|
| reveal   | 拂尘 | 轻拂尘埃，唤回微光 |
| assemble | 拼合 | 将碎片拖回原位，拼起旧忆 |
| choose   | 抉择 | 停一停，听一听自己的心声 |
| archive  | 归档 | 尘埃落定，微光终有归处 |

> 文案由 flow 经 `ShowStep(index,total,title,hint)` 推给视图，视图直接写 `Text`；design 改词只需改 `OnboardingHints.cs`，无需动 Canvas。

## 交互
- **下一步**：`Btn_Next.onClick → bridge.AdvanceStep()`。最后一步按钮文案建议改为"开始"（视图内判断 `_current == total-1`）。
- **跳过**：`Btn_Skip.onClick → bridge.SkipAll()`。
- 走完 4 步或点跳过 → `OnCompleted()`：收起所有面板 + 隐藏 Canvas；`PlayerPrefs` 写 `weiguang.onboarding.done=1`。

## 动画（TODO，本机补）
- 入场：面板淡入 + 轻微上移（DoTween 或 CanvasGroup + DOTween/协程）。
- 切换：旧面板淡出、新面板淡入。
- 收尾：整体淡出后 `gameObject.SetActive(false)`。

## 持久化
- key：`weiguang.onboarding.done`（PlayerPrefs，int 0/1）。
- 已引导过（`IsOnboarded()==true`）再次进游戏，`EVT_FIRST_LAUNCH` 仍会广播，但 `OnboardingFlow.Start` 直接 return，**不弹**。

## 验收口径（本机手动）
1. 清档首启（删 `PlayerPrefs` 的 `weiguang.onboarding.done`，或重装）：进游戏应弹"拂尘"引导（第 1/4 步）。
2. 点"下一步"依次走 reveal→assemble→choose→archive，每步文案与表一致。
3. 第 4 步点"下一步"/任意步点"跳过" → Canvas 收起。
4. 重启游戏（不删档）：**不再弹**首启引导（证明持久化生效）。
5. EditMode 测试 `OnboardingFlowTests` 全绿（沙箱已保）。

## 与 C1 的协同
- 首启引导与降级档位无关（引导在任意设备都弹一次）；C1 只影响拂尘 Shader 采样密度。
- 若低端机首启引导期间帧率掉，可一并降低引导动画密度（不在本次范围）。
