// AssembleVisualBridge.cs — B2 拼合吸附表现桥（订阅 EVT_ASSEMBLE_COMPLETE）
// 作者：林绘澄（art-director）｜Phase 6-A
// 职责：订阅 EVT_ASSEMBLE_COMPLETE，播放碎片吸附收束动效 stub（轻颤归位 + 物件整体微光一闪）。
//       不轮询锁定状态（契约：见 art-spec.md §2.3）。美术表现留 TODO，无资产不崩。
using UnityEngine;
using Weiguang.Core;

namespace Weiguang.Runtime.ArtBinding
{
    /// <summary>
    /// 挂载到 AssemblyBoard 拼合盘 GameObject。payload: AssembleCompleteEvent(locked,total)。
    /// </summary>
    public class AssembleVisualBridge : ArtBridgeBase
    {
        [Tooltip("收束微光闪现时序数（秒）")]
        public float convergeFlashSec = 0.35f;

        float _flashStart = -1f;
        UnityEngine.Material _boardMat;

        protected override void OnBind()
        {
            Bus.Subscribe(GameEvents.EVT_ASSEMBLE_COMPLETE, OnAssembleComplete);
            var r = GetComponent<Renderer>();
            _boardMat = r != null ? r.material : null;
        }

        protected override void OnUnbind()
        {
            Bus.Unsubscribe(GameEvents.EVT_ASSEMBLE_COMPLETE, OnAssembleComplete);
        }

        void OnAssembleComplete(object payload)
        {
            // locked_count == total_count 时由逻辑层已保证（见 SessionRunner.StartAssemble）。
            if (payload is AssembleCompleteEvent ev)
                Debug.Log($"[ArtBinding/Assemble] 吸附收束 {ev.locked_count}/{ev.total_count}");
            _flashStart = Time.unscaledTime;
            // TODO(美术表现): 触发碎片轻颤回弹 + 物件整体微光一闪（可复用 DustReveal._Pulse 通道）。
            // TODO(音频): 播 SFX-02 拼合咔哒（按物件 material 微调 Glass/Wood/Paper）。
        }

        void Update()
        {
            if (_flashStart < 0f || _boardMat == null) return;
            float t = (Time.unscaledTime - _flashStart) / convergeFlashSec;
            if (t >= 1f) { _flashStart = -1f; return; }
            // 单次衰减闪（无需 0→1→0，收束为一次性）
            _boardMat.SetFloat("_Pulse", 1f - t);
        }
    }
}
