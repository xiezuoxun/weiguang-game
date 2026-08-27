// OnboardingFlow.cs — 打磨 Phase 7-C2：首启引导流程（纯 C# 逻辑，可 EditMode 测）。
// 设计：把"四动词引导"的流转（reveal→assemble→choose→archive）与具体 UI 解耦——
//   视图只实现 IOnboardingView（展示某步 / 完成），持久化只实现 IOnboardingStore（是否已引导 / 标记完成）。
//   本类编排流转，不依赖任何 Unity UI 类型，故可被 EditMode 测试直接驱动（无需 Canvas）。
//   真实 Canvas 由本机 OnboardingCanvasView : MonoBehaviour, IOnboardingView 接（见 phase7-onboarding-canvas.md）。
using System;
using System.Collections.Generic;
using UnityEngine;
using Weiguang.Core;

namespace Weiguang.Runtime.Onboarding
{
    /// <summary>首启引导视图（UI 层实现）。桥/测试注入不同实现：
    ///   - LogOnboardingView：默认，仅日志（保留原 stub 行为，无 Canvas 不崩）；
    ///   - OnboardingCanvasView：本机 MonoBehaviour，驱动真实 Canvas 面板。</summary>
    public interface IOnboardingView
    {
        /// <summary>展示第 index 步（共 total 步），title/hint 取自 OnboardingHints。</summary>
        void ShowStep(int index, int total, string title, string hint);
        /// <summary>引导走完（前进到底或跳过）后调用，视图收起。</summary>
        void OnCompleted();
    }

    /// <summary>首启引导持久化（已引导过则不再弹）。默认 PlayerPrefsOnboardingStore；测试注入内存替身。</summary>
    public interface IOnboardingStore
    {
        bool IsOnboarded();
        void MarkOnboarded();
    }

    /// <summary>默认视图：纯日志（保留原 stub 行为），并记录已展示步骤供断言。</summary>
    public class LogOnboardingView : IOnboardingView
    {
        public readonly List<(int index, string title, string hint)> Shown = new List<(int, string, string)>();
        public int CompletedCount;

        public void ShowStep(int index, int total, string title, string hint)
        {
            Shown.Add((index, title, hint));
            Debug.Log($"[Onboarding] 首启引导（{index + 1}/{total}）：{title} —— {hint}");
        }
        public void OnCompleted()
        {
            CompletedCount++;
            Debug.Log("[Onboarding] 首启引导完成（LogOnboardingView 收起）");
        }
    }

    /// <summary>默认持久化：PlayerPrefs（本机运行用）。EditMode 测试不碰它（注入内存替身）。</summary>
    public class PlayerPrefsOnboardingStore : IOnboardingStore
    {
        const string KEY = "weiguang.onboarding.done";
        public bool IsOnboarded() => UnityEngine.PlayerPrefs.GetInt(KEY, 0) == 1;
        public void MarkOnboarded() => UnityEngine.PlayerPrefs.SetInt(KEY, 1);
    }

    /// <summary>四动词首启引导流转编排（纯 C#）。</summary>
    public class OnboardingFlow
    {
        readonly IOnboardingView _view;
        readonly IOnboardingStore _store;
        List<(string title, string hint)> _steps;
        int _index = -1;

        /// <summary>是否处于引导进行中（已弹且未走完）。</summary>
        public bool Active { get; private set; }

        public OnboardingFlow(IOnboardingView view, IOnboardingStore store)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>收到 EVT_FIRST_LAUNCH：已引导过则忽略；否则按 GDD 顺序弹四动词第一步。</summary>
        public void Start(FirstLaunchEvent ev)
        {
            if (_store.IsOnboarded()) return; // 已引导过：不弹（幂等）
            _steps = new List<(string, string)>
            {
                (ev.reveal.Key, ev.reveal.Value),
                (ev.assemble.Key, ev.assemble.Value),
                (ev.choose.Key, ev.choose.Value),
                (ev.archive.Key, ev.archive.Value)
            };
            _index = 0;
            Active = true;
            _view.ShowStep(0, _steps.Count, _steps[0].title, _steps[0].hint);
        }

        /// <summary>玩家点"下一步"：前进到下一动词；走完则收尾持久化。</summary>
        public void AdvanceStep()
        {
            if (!Active) return;
            _index++;
            if (_index >= _steps.Count) { Complete(); return; }
            _view.ShowStep(_index, _steps.Count, _steps[_index].title, _steps[_index].hint);
        }

        /// <summary>玩家点"跳过"：直接收尾持久化。</summary>
        public void SkipAll()
        {
            if (Active) Complete();
        }

        void Complete()
        {
            Active = false;
            _store.MarkOnboarded();   // 持久化：下次进游戏不再弹
            _view.OnCompleted();
        }
    }
}
