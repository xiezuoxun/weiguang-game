// ArtBindingContext.cs — ArtBinding 共享上下文（EventBus 注入点）
// 作者：林绘澄（art-director）｜Phase 6-A
// 设计：Core EventBus 无静态单例（ADR-005 解耦），Runtime 层由 GameBootstrap 拥有 EventBus 实例。
// 美术桥接脚本（*VisualBridge）在场景中被 GameBootstrap 实例化/注入，故统一通过 Bind(bus) 注入，
// 而非自建静态单例（避免与 GameBootstrap 的 _bus 语义冲突）。
using Weiguang.Core;

namespace Weiguang.Runtime.ArtBinding
{
    /// <summary>
    /// 桥接脚本基类：统一持有 EventBus 引用并管理订阅/退订生命周期。
    /// 子类实现 OnBind（订阅）与 OnUnbind（退订）。由 engineering-lead 在 6-B 阶段
    /// 于 GameBootstrap.Awake 末尾对所有 VisualBridge 调用 Bind(_bus)。
    /// 打磨（Phase 7-C1）：Bind 增 quality 重载，把运行时降级配置一并注入，
    /// 需读取降级档位的桥（如 RevealVisualBridge）直接用 <see cref="Quality"/>。
    /// </summary>
    public abstract class ArtBridgeBase : UnityEngine.MonoBehaviour
    {
        protected EventBus Bus { get; private set; }

        /// <summary>当前降级档位（由 GameBootstrap 按设备推断注入）。为 null 表示"全开默认档"。</summary>
        protected RuntimeQuality Quality { get; private set; }

        /// <summary>由宿主（GameBootstrap）注入 EventBus 并触发订阅。幂等。</summary>
        public void Bind(EventBus bus) => Bind(bus, null);

        /// <summary>由宿主（GameBootstrap）注入 EventBus + 降级配置并触发订阅。幂等。</summary>
        public void Bind(EventBus bus, RuntimeQuality quality)
        {
            if (bus == null) return;
            if (Bus != null && Bus == bus && Equals(Quality, quality)) return; // 已绑定同实例同档位
            if (Bus != null) OnUnbind();           // 切换实例先退订
            Bus = bus;
            Quality = quality;
            OnBind();
        }

        protected abstract void OnBind();
        protected abstract void OnUnbind();

        void OnDestroy() => OnUnbind();
    }
}
