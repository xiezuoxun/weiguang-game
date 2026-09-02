// OnboardingCanvasView.cs — 打磨 Phase 7-C2：首启引导真实 Canvas 视图。
// 作者：程基岩（engineering-lead）｜Phase 7-C2
// 职责：实现 IOnboardingView，把 OnboardingFlow 的四动词引导渲染到真实 Canvas 面板；
//       并把"下一步/跳过"按钮接到 OnboardingUIRuntimeBridge.AdvanceStep/SkipAll。
// 动画（本机补全）：面板入场淡入+上移（≤250ms，对齐 C2 验收节奏）；最后一步按钮改"开始"；
//                  收尾整体淡出后收起 Canvas。全部无外部依赖（CanvasGroup + 协程补间）。
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Weiguang.Core;
using Weiguang.Runtime.ArtBinding;

namespace Weiguang.Runtime.Onboarding
{
    /// <summary>
    /// 挂在首启引导 Canvas（或 UIRoot 下的引导容器）上；Inspector 绑定 4 个步骤面板 + 文案 + 按钮。
    /// Awake 找到 OnboardingUIRuntimeBridge 并接线按钮；ShowStep/OnCompleted 由 flow 经 bridge 事件驱动。
    /// 面板动画走本组件协程：容器 CanvasGroup 淡入淡出 + 活动面板 RectTransform 上移回位。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class OnboardingCanvasView : MonoBehaviour, IOnboardingView
    {
        [Header("步骤面板（reveal/assemble/choose/archive，顺序一致）")]
        public GameObject[] stepPanels;        // 4 个面板，索引对应四动词
        public Text[] stepTitles;              // 每步标题（也可直接放面板内，二选一）
        public Text[] stepHints;               // 每步副提示

        [Header("按钮")]
        public Button nextButton;              // 下一步（最后一步文案自动改"开始"）
        public Button skipButton;              // 跳过
        [Tooltip("下一步按钮的文案 Text（留空则自动取 nextButton 子级首个 Text）")]
        public Text nextButtonLabel;

        [Header("动画参数（C2 验收：入场 ≤250ms）")]
        [Tooltip("面板入场时长(秒)")]
        public float stepFadeSeconds = 0.25f;
        [Tooltip("面板入场上移像素（基准 1080 宽）")]
        public float liftUpPx = 24f;

        [Header("运行时引用（自动查找）")]
        public OnboardingUIRuntimeBridge bridge; // 同场景内的引导桥

        CanvasGroup _group;
        int _current = -1;

        void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (bridge == null) bridge = FindObjectOfType<OnboardingUIRuntimeBridge>();
            if (nextButton != null) nextButton.onClick.AddListener(() => bridge?.AdvanceStep());
            if (skipButton != null) skipButton.onClick.AddListener(() => bridge?.SkipAll());
            if (nextButtonLabel == null && nextButton != null)
                nextButtonLabel = nextButton.GetComponentInChildren<Text>();
            // 初始隐藏所有面板（引导未触发前不可见）
            SetAllPanels(false);
            _group.alpha = 0f;
        }

        public void ShowStep(int index, int total, string title, string hint)
        {
            _current = index;
            StopAllCoroutines(); // 打断上一步入场/收尾补间，直接切新步
            SetAllPanels(false);
            if (stepPanels != null && index < stepPanels.Length && stepPanels[index] != null)
                stepPanels[index].SetActive(true);
            if (stepTitles != null && index < stepTitles.Length && stepTitles[index] != null)
                stepTitles[index].text = title;
            if (stepHints != null && index < stepHints.Length && stepHints[index] != null)
                stepHints[index].text = hint;
            // 最后一步按钮文案改"开始"，其余"下一步"
            if (nextButtonLabel != null)
                nextButtonLabel.text = index >= total - 1 ? "开始" : "下一步";
            gameObject.SetActive(true);
            if (isActiveAndEnabled)
                StartCoroutine(AnimateStepIn(stepPanels != null && index < stepPanels.Length ? stepPanels[index] : null));
            else
                _group.alpha = 1f; // 协程不可用（组件禁用等）：无动画直接显示
        }

        public void OnCompleted()
        {
            StopAllCoroutines();
            if (isActiveAndEnabled)
            {
                StartCoroutine(AnimateOut());   // 淡出后收起
                return;
            }
            SetAllPanels(false);
            gameObject.SetActive(false);
        }

        // ── 入场：容器 0→1 淡入 + 活动面板自 liftUpPx 上移回位（保留面板原始位置）──
        IEnumerator AnimateStepIn(GameObject panel)
        {
            RectTransform rt = panel != null ? panel.transform as RectTransform : null;
            Vector3 basePos = rt != null ? rt.localPosition : Vector3.zero;
            Vector3 offset = Vector3.down * liftUpPx;
            if (rt != null) rt.localPosition = basePos + offset; // 起始下移
            float t = 0f;
            while (t < stepFadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / stepFadeSeconds);
                _group.alpha = k;
                if (rt != null) rt.localPosition = basePos + offset * (1f - k); // 下移量随进度归零
                yield return null;
            }
            _group.alpha = 1f;
            if (rt != null) rt.localPosition = basePos; // 精确回位
        }

        // ── 收尾：容器 1→0 淡出后收起全部面板 ──
        IEnumerator AnimateOut()
        {
            float t = 0f;
            while (t < stepFadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = 1f - Mathf.Clamp01(t / stepFadeSeconds);
                yield return null;
            }
            _group.alpha = 0f;
            SetAllPanels(false);
            gameObject.SetActive(false); // 收起整个引导 Canvas
        }

        void SetAllPanels(bool visible)
        {
            if (stepPanels == null) return;
            foreach (var p in stepPanels) if (p != null) p.SetActive(visible);
        }
    }
}
