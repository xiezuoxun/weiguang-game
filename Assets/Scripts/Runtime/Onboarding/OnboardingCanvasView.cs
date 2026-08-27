// OnboardingCanvasView.cs — 打磨 Phase 7-C2：首启引导真实 Canvas 视图（本机 Unity 内补全）。
// 作者：程基岩（engineering-lead）｜Phase 7-C2
// 职责：实现 IOnboardingView，把 OnboardingFlow 的四动词引导渲染到真实 Canvas 面板；
//       并把"下一步/跳过"按钮接到 OnboardingUIRuntimeBridge.AdvanceStep/SkipAll。
// 本文件为**可编译骨架**：public 字段在 Inspector 绑定，面板内容与动画 TODO 由美术/UI 在本机补全。
// 沙箱不可跑 Unity，故仅作结构占位；完整验收见 game/production/phase7-onboarding-canvas.md。
using UnityEngine;
using UnityEngine.UI;
using Weiguang.Core;
using Weiguang.Runtime.ArtBinding;

namespace Weiguang.Runtime.Onboarding
{
    /// <summary>
    /// 挂在首启引导 Canvas（或 UIRoot 下的引导容器）上；Inspector 绑定 4 个步骤面板 + 文案 + 按钮。
    /// Awake 找到 OnboardingUIRuntimeBridge 并接线按钮；ShowStep/OnCompleted 由 flow 经 bridge 事件驱动。
    /// </summary>
    public class OnboardingCanvasView : MonoBehaviour, IOnboardingView
    {
        [Header("步骤面板（reveal/assemble/choose/archive，顺序一致）")]
        public GameObject[] stepPanels;        // 4 个面板，索引对应四动词
        public Text[] stepTitles;              // 每步标题（也可直接放面板内，二选一）
        public Text[] stepHints;               // 每步副提示

        [Header("按钮")]
        public Button nextButton;              // 下一步
        public Button skipButton;              // 跳过

        [Header("运行时引用（自动查找）")]
        public OnboardingUIRuntimeBridge bridge; // 同场景内的引导桥

        int _current = -1;

        void Awake()
        {
            if (bridge == null) bridge = FindObjectOfType<OnboardingUIRuntimeBridge>();
            if (nextButton != null) nextButton.onClick.AddListener(() => bridge?.AdvanceStep());
            if (skipButton != null) skipButton.onClick.AddListener(() => bridge?.SkipAll());
            // TODO(UI): 初始隐藏所有面板（引导未触发前不可见）
            SetAllPanels(false);
        }

        public void ShowStep(int index, int total, string title, string hint)
        {
            _current = index;
            SetAllPanels(false);
            if (stepPanels != null && index < stepPanels.Length && stepPanels[index] != null)
                stepPanels[index].SetActive(true);
            if (stepTitles != null && index < stepTitles.Length && stepTitles[index] != null)
                stepTitles[index].text = title;
            if (stepHints != null && index < stepHints.Length && stepHints[index] != null)
                stepHints[index].text = hint;
            // TODO(UI): 面板入场动画（淡入/上移）；nextButton 文案在最后一步改为"开始"
            gameObject.SetActive(true);
        }

        public void OnCompleted()
        {
            SetAllPanels(false);
            gameObject.SetActive(false); // 收起整个引导 Canvas
            // TODO(UI): 收尾动画后隐藏；可触发首屏正式进入
        }

        void SetAllPanels(bool visible)
        {
            if (stepPanels == null) return;
            foreach (var p in stepPanels) if (p != null) p.SetActive(visible);
        }
    }
}
