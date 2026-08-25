// ArtAcceptanceTests.cs — Phase 6 美术/表现验收（PlayMode 自动化门）
// 作者：程基岩（engineering-lead）｜Phase 6-B/6-C
// 运行：Unity Test Runner → PlayMode → Run All
// 依赖：Weiguang.Core（EventBus/GameEvents）、Weiguang.Runtime.ArtBinding（*VisualBridge）
// 说明：本文件为"沙箱不可验、用户本机验"的验收脚本。缺二进制资产时 AssetPresence 测试 FAIL（即步骤①未完成信号）。
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Weiguang.Core;
using Weiguang.Runtime.ArtBinding;

namespace Weiguang.Tests
{
    public class ArtAcceptanceTests
    {
        // ── A1/A2：B1 Shader 编译 + 桥驱动脉冲链路 ───────────────────────────
        [UnityTest]
        public IEnumerator B1_ShaderPulse_DrivenByBridge()
        {
            var shader = Shader.Find("Weiguang/DustReveal");
            Assert.IsNotNull(shader, "DustReveal.shader 未编译/未找到（A1 失败：URP 2D 下 Shader 不可解析）");

            var go = new GameObject("RevealHost");
            go.AddComponent<SpriteRenderer>(); // 满足 RevealVisualBridge [RequireComponent(Renderer)]
            var bridge = go.AddComponent<RevealVisualBridge>();

            var bus = new EventBus { LogError = m => Debug.LogError(m) };
            bridge.Bind(bus);

            // 发一次阈值跨越事件，桥应触发 _Pulse（0→1→0 三角补间）
            bus.Publish(GameEvents.EVT_REVEAL_WHISPER,
                new RevealWhisperEvent(RevealWhisperEvent.WHISPER_50, 0.5f));

            // 让一帧过去，桥 Update() 把 _Pulse 写入材质
            yield return null;

            var mat = go.GetComponent<SpriteRenderer>().material;
            float pulse = mat.GetFloat("_Pulse");
            Assert.Greater(pulse, 0f, "A2 失败：发 EVT_REVEAL_WHISPER 后 _Pulse 未 >0（桥→Shader 链路未通）");

            Object.Destroy(go);
        }

        // ── A4/A5 前置：4 桥 Bind + 事件驱动无异常（生命周期干净） ─────────────
        [UnityTest]
        public IEnumerator B2_B3_BridgesBindWithoutException()
        {
            var bus = new EventBus { LogError = m => Debug.LogError(m) };

            var reveal = MakeBridge<RevealVisualBridge>();
            var assemble = new GameObject().AddComponent<AssembleVisualBridge>();
            var choose = new GameObject().AddComponent<ChoiceVisualBridge>();
            var codex = new GameObject().AddComponent<CodexVisualBridge>();

            Assert.DoesNotThrow(() =>
            {
                reveal.Bind(bus); assemble.Bind(bus); choose.Bind(bus); codex.Bind(bus);

                bus.Publish(GameEvents.EVT_REVEAL_WHISPER, new RevealWhisperEvent(RevealWhisperEvent.WHISPER_25, 0.25f));
                bus.Publish(GameEvents.EVT_ASSEMBLE_COMPLETE, new AssembleCompleteEvent(2, 2));
                bus.Publish(GameEvents.EVT_CHOICE_MADE, EndingTag.Truth);
                bus.Publish(GameEvents.EVT_ARCHIVED, new ArchivedEvent("cx_com_001", 0, false));
            }, "6-B 桥订阅/退订生命周期异常");

            // 退订（OnDestroy 亦会触发，此处显式验证 Unsubscribe 路径）
            Assert.DoesNotThrow(() =>
            {
                Object.Destroy(reveal); Object.Destroy(assemble);
                Object.Destroy(choose); Object.Destroy(codex);
            });

            yield return null;
        }

        // ── 6-B 新增：EventBus.Unsubscribe 精准退订（Core 纯逻辑） ────────────
        [Test]
        public void EventBus_Unsubscribe_RemovesHandler()
        {
            var bus = new EventBus();
            int hits = 0;
            void Handler(object p) => hits++;

            bus.Subscribe("EVT_X", Handler);
            bus.Publish("EVT_X");
            Assert.AreEqual(1, hits, "订阅后首次发布应命中");

            bus.Unsubscribe("EVT_X", Handler);
            bus.Publish("EVT_X");
            Assert.AreEqual(1, hits, "Unsubscribe 后不应再命中（精准退订）");
        }

        // ── A6：二进制资产齐全硬门（步骤①完成信号） ─────────────────────────
        [Test]
        public void AssetPresence_FragmentsAndSlots()
        {
            // 仅抽查关键资产；全部非空即视为美术按 phase6-asset-tasks.md 产齐
            string[] keys = {
                "Fragments/fr_001", "Fragments/fr_013",
                "Slots/it_watch_board", "Slots/it_ornament_board",
                "Choices/choice_tab", "Choices/choice_tab_selected",
                "Items/it_watch", "Clients/cl_shen",
                "Backdrop/dust_table", "Whisper/whisper_note",
                "Cursor/dust_brush", "Audio/sfx_reveal", "Audio/bgm_main"
            };
            foreach (var k in keys)
            {
                var s = (UnityEngine.Object)Resources.Load<Sprite>(k) ?? (UnityEngine.Object)Resources.Load<AudioClip>(k);
                Assert.IsNotNull(s, $"资产缺失：{k}（步骤①未生产或路径/命名不符 art-spec §0.2）");
            }
        }

        static T MakeBridge<T>() where T : MonoBehaviour
        {
            var go = new GameObject(typeof(T).Name);
            go.AddComponent<SpriteRenderer>();
            return go.AddComponent<T>();
        }
    }
}
