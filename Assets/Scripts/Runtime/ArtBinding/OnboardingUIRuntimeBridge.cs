// OnboardingUIRuntimeBridge.cs — 首启引导桥（打磨 Phase 7-C2）
// 职责：订阅 EVT_FIRST_LAUNCH，交给纯 C# OnboardingFlow 编排四动词引导流转；
//       本类只做事件→flow 的转发，并暴露 AdvanceStep()/SkipAll() 供本机 UI 按钮调用（ADR-005 解耦）。
// 真实 Canvas 由本机 OnboardingCanvasView : MonoBehaviour, IOnboardingView 接（见 phase7-onboarding-canvas.md）。
// 由 GameBootstrap.BindArtBridges 经 FindObjectsOfType<ArtBridgeBase>() 自动扫到并 Bind(_bus, quality)。
using Weiguang.Core;
using Weiguang.Runtime.Onboarding;

namespace Weiguang.Runtime.ArtBinding
{
    /// <summary>
    /// 挂载到场景内任意 GameObject（建议挂在 UIRoot）；Awake 后由 GameBootstrap 注入 EventBus。
    /// 仅消费 EVT_FIRST_LAUNCH，不反向依赖任何玩法系统。
    /// </summary>
    public class OnboardingUIRuntimeBridge : ArtBridgeBase
    {
        OnboardingFlow _flow;

        protected override void OnBind()
        {
            // 默认视图/持久化（无 Canvas 时仅日志、不崩；本机可注入真实 Canvas 视图）。
            _flow = new OnboardingFlow(new LogOnboardingView(), new PlayerPrefsOnboardingStore());
            Bus.Subscribe(GameEvents.EVT_FIRST_LAUNCH, OnFirstLaunch);
        }

        protected override void OnUnbind()
        {
            Bus.Unsubscribe(GameEvents.EVT_FIRST_LAUNCH, OnFirstLaunch);
        }

        void OnFirstLaunch(object payload)
        {
            if (payload is FirstLaunchEvent ev) _flow.Start(ev);
        }

        /// <summary>供本机 UI 层"下一步"按钮调用：推动四动词引导前进。</summary>
        public void AdvanceStep() => _flow?.AdvanceStep();

        /// <summary>供本机 UI 层"跳过"按钮调用：直接收尾并持久化（下次不再弹）。</summary>
        public void SkipAll() => _flow?.SkipAll();
    }
}
