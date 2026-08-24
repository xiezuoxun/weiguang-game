// RuntimeQuality.cs — 打磨阶段新增：低端机/移动端降级开关（纯 C#，可序列化）。
// 由 GameBootstrap 在 Awake 按设备档位设默认值，SessionRunner / 美术 Shader 读取以降级表现。
// 不依赖 UnityEngine（保持 Runtime 层可被 EditMode 测试引用），用 System 序列化标记。
using System;

namespace Weiguang.Runtime
{
    /// <summary>运行时画质/性能降级配置。所有项均为安全降级（关掉仍是完整玩法，只是表现从简）。
    /// 默认档位由 GameBootstrap 按设备性能推断；也可由设置界面暴露给玩家手动调。</summary>
    [Serializable]
    public class RuntimeQuality
    {
        /// <summary>是否启用拂尘微光 Shader（B1）。低端机可关，退化为纯色显隐。</summary>
        public bool enableGlowShader = true;

        /// <summary>拂尘网格最大采样格数（DustGrid 分辨率上限）。越低越省（移动端/低端机降 8×8→6×6）。</summary>
        public int maxDustCells = 64;

        /// <summary>是否启用图鉴解锁动画（EVT_CODEX_UNLOCKED 驱动的表现）。关掉则直接静态显示。</summary>
        public bool enableCodexAnim = true;

        /// <summary>抉择纸签高亮 Shader（B3）。关掉退化为纯色块高亮。</summary>
        public bool enableChoiceShader = true;

        /// <summary>按粗略设备档位给一套保守默认（移动端/低内存机降级）。</summary>
        public static RuntimeQuality ForDevice(bool isMobile, int systemMemoryMB)
        {
            var q = new RuntimeQuality();
            if (isMobile)
            {
                q.enableGlowShader = systemMemoryMB >= 2048;
                q.maxDustCells = systemMemoryMB >= 2048 ? 64 : 36;
                q.enableChoiceShader = systemMemoryMB >= 1536;
            }
            return q;
        }
    }
}
