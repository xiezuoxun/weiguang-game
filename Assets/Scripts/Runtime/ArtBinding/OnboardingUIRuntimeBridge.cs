// OnboardingUIRuntimeBridge.cs — 首启引导 UI 桥（打磨 Phase 6-C）
// 作者：程基岩（engineering-lead）｜Phase 6-C
// 职责：订阅 EVT_FIRST_LAUNCH，把四动词引导文案（reveal/assemble/choose/archive）转成步骤列表；
//       当前为纯 C# stub（不引用任何 Unity UI 类型），仅 Debug.Log 每条引导，保证无 Canvas/资产时不崩核心循环。
//       真实首启引导 Canvas/面板由 UI 层后续接（见 ShowOnboarding 的 TODO）。
// 由 GameBootstrap.BindArtBridges 经 FindObjectsOfType<ArtBridgeBase>() 自动扫到并 Bind(_bus) —— 无需改 GameBootstrap。
using System.Collections.Generic;
using UnityEngine;
using Weiguang.Core;
using Weiguang.Runtime;

namespace Weiguang.Runtime.ArtBinding
{
    /// <summary>
    /// 挂载到场景内任意 GameObject（建议挂在 UIRoot）；Awake 后由 GameBootstrap 注入 EventBus。
    /// 仅消费 EVT_FIRST_LAUNCH，不反向依赖任何玩法系统（ADR-005 解耦）。
    /// </summary>
    public class OnboardingUIRuntimeBridge : ArtBridgeBase
    {
        protected override void OnBind()
        {
            Bus.Subscribe(GameEvents.EVT_FIRST_LAUNCH, OnFirstLaunch);
        }

        protected override void OnUnbind()
        {
            Bus.Unsubscribe(GameEvents.EVT_FIRST_LAUNCH, OnFirstLaunch);
        }

        void OnFirstLaunch(object payload)
        {
            if (!(payload is FirstLaunchEvent ev)) return;
            // 四动词引导（reveal/assemble/choose/archive）按 GDD 顺序拼成步骤列表
            var steps = new List<KeyValuePair<string, string>>
            {
                ev.reveal, ev.assemble, ev.choose, ev.archive
            };
            ShowOnboarding(steps);
        }

        /// <summary>纯 C# stub：不引用任何 Unity UI 类型，保持 Runtime 层可被 EditMode 测试引用且不崩核心循环。
        /// 当前仅记日志；真实面板接好后改为按 steps 顺序展示四动词引导。</summary>
        void ShowOnboarding(List<KeyValuePair<string, string>> steps)
        {
            if (steps == null) return;
            foreach (var kv in steps)
                Debug.Log($"[Onboarding] 首启引导：{kv.Key} —— {kv.Value}");
            // TODO(UI): 接真实首启引导 Canvas/面板，按 steps 顺序展示四动词引导
        }
    }
}
