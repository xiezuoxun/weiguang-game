// ChoiceVisualBridge.cs — B3 抉择纸签选中表现桥（订阅 EVT_OPTION_SELECTED / EVT_CHOICE_MADE）
// 作者：林绘澄（art-director）｜Phase 6-A
// 职责：订阅 EVT_OPTION_SELECTED（落定选中）与 EVT_OPTION_HIGHLIGHTED（悬停高亮），
//       驱动纸签选中高亮（浮起 + 描边加粗 + 微光；色弱友好：形状/描边/亮度三重区分）。
//       命中热区由 ChoiceHitTester(1.3×/0.6) 处理，本桥只管视觉。美术表现留 TODO，无资产不崩。
using UnityEngine;
using Weiguang.Core;

namespace Weiguang.Runtime.ArtBinding
{
    /// <summary>
    /// 挂载到 ChoiceBoard 抉择容器。纸签 GameObject 命名为 option_&lt;option_id&gt; 以便定位。
    /// 选中高亮时限 ≤ 250ms（验收 §3.4）；_liftPx 默认 10px（验收区间 8–12）。
    /// </summary>
    public class ChoiceVisualBridge : ArtBridgeBase
    {
        [Tooltip("选中纸签浮起像素(基准1080宽)，验收 8–12")]
        public float liftPx = 10f;
        [Tooltip("高亮/选中动效时长(ms)，验收 ≤250")]
        public float animMs = 220f;

        // option_id -> 纸签 Transform 缓存（由 engineering-lead 6-B 在实例化纸签时登记）
        readonly System.Collections.Generic.Dictionary<string, Transform> _tabs =
            new System.Collections.Generic.Dictionary<string, Transform>();

        public void RegisterTab(string optionId, Transform tab) { if (optionId != null) _tabs[optionId] = tab; }

        protected override void OnBind()
        {
            Bus.Subscribe(GameEvents.EVT_OPTION_SELECTED, OnSelected);
            Bus.Subscribe(GameEvents.EVT_OPTION_HIGHLIGHTED, OnHighlighted);
            Bus.Subscribe(GameEvents.EVT_CHOICE_MADE, OnChoiceMade);
        }

        protected override void OnUnbind()
        {
            Bus.Unsubscribe(GameEvents.EVT_OPTION_SELECTED, OnSelected);
            Bus.Unsubscribe(GameEvents.EVT_OPTION_HIGHLIGHTED, OnHighlighted);
            Bus.Unsubscribe(GameEvents.EVT_CHOICE_MADE, OnChoiceMade);
        }

        void OnSelected(object payload)
        {
            if (payload is ChoiceOptionEvent ev && ev.type == ChoiceOptionEvent.TYPE_SELECTED)
                ApplyHighlight(ev.option_id, true);
        }

        void OnHighlighted(object payload)
        {
            if (payload is ChoiceOptionEvent ev && ev.type == ChoiceOptionEvent.TYPE_HIGHLIGHTED)
                ApplyHighlight(ev.option_id, false); // 悬停态（弱高亮），落定态覆盖
        }

        void OnChoiceMade(object payload)
        {
            // EVT_CHOICE_MADE 用于驱动后续流转（已发 EVT_OPTION_SELECTED 先于本事件）；
            // 此处可触发纸响 SFX-03（TODO 音频层）。
            // TODO(音频): 播 SFX-03 抉择纸响。
        }

        void ApplyHighlight(string optionId, bool selected)
        {
            if (!_tabs.TryGetValue(optionId, out var tab)) { Debug.LogWarning($"[ArtBinding/Choice] 未登记纸签 {optionId}"); return; }
            // TODO(美术表现): 选中态 Shader 置 _Selected=1 + 浮起 liftPx + 描边呼吸；未选中复位。
            // 色弱友好：必须叠加形状/描边/亮度，非纯色。
            Debug.Log($"[ArtBinding/Choice] 高亮 option={optionId} selected={selected}");
        }
    }
}
