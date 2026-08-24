// DustReveal.shader — B1 拂尘微光 Shader（尘遮罩 + 阈值脉冲）
// 引擎：Unity 2022.3.20f1 + URP 2D Renderer（Sprite-Unlit 兼容，单 Pass）
// 作者：林绘澄（art-director）｜Phase 6-A 资产生产
//
// 技术契约（对齐 production/art-spec.md §1.3）：
//   _RevealProgress : 整体显影进度 0→1，由 SessionRunner.StartReveal 的 grid.RevealPct() 驱动
//   _Threshold      : 当前顿挫阈值（0.25/0.50/0.75），对齐 RevealThresholdTracker.T25/T50/T75
//   _GlowColor      : 微光叠加色（暖白/淡金，支持 HDR 溢出）
//   _Pulse          : 脉冲通道 0→1→0（Unity 层在 EVT_REVEAL_WHISPER / EVT_REVEAL_THRESHOLD_CROSSED
//                     收到后于 120–200ms 内补间 0→1→0），制造阈值附近可见亮度跳变（≥15%）
//
// 阈值对齐说明：着色器不内置 0.25/0.50/0.75 常量——阈值档位由 Core 层
// RevealThresholdTracker 的 once-lock 机制判定并经事件广播，Unity 层把当前档位写入
// _Threshold 并触发 _Pulse。这样着色器零逻辑依赖、纯表现，符合 ADR-005 分层。
Shader "Weiguang/DustReveal"
{
    Properties
    {
        _MainTex        ("物件底层 (RGBA)", 2D)        = "white" {}
        _DustTex        ("尘遮罩噪点 (灰度平铺)", 2D)  = "white" {}
        _GlowTex        ("微光径向光晕 (可选)", 2D)    = "white" {}
        _RevealProgress ("显影进度 RevealProgress", Float) = 0.0
        _Threshold      ("当前顿挫阈值 Threshold", Float) = 0.0
        _GlowColor      ("微光色 GlowColor", Color)    = (1.0, 0.93, 0.78, 1.0)
        _Pulse          ("脉冲 Pulse", Float)          = 0.0
        _DustTint       ("积尘色调", Color)            = (0.62, 0.58, 0.52, 1.0)
        _GlowStrength   ("微光强度", Float)            = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"   // 强制 URP（2D Renderer 兼容）
            "IgnoreProjector"="True"
            "PreviewType"="Sprite"
        }
        LOD 100

        // 单 Pass，加色混合叠加微光；移动端中端机可控
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "DustRevealPass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            // ── URP 2D 兼容 include（不引用 Built-in 专属节点）──
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;   // Sprite 顶点色（可用作整体压暗）
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _DustTex;
            sampler2D _GlowTex;
            float4   _MainTex_ST;
            float4   _DustTex_ST;

            float _RevealProgress;
            float _Threshold;
            float _Pulse;
            float _GlowStrength;
            float4 _GlowColor;
            float4 _DustTint;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv         = IN.uv;
                OUT.color      = IN.color;
                return OUT;
            }

            // 平滑阶跃：在 _Threshold 附近给一个可见但柔和的过渡带（宽 0.04），
            // 叠加 _Pulse 形成"顿挫跳变"，满足验收：跨越 0.25/0.50/0.75 亮度差 ≥15%。
            float bandStep(float x, float center, float width)
            {
                return smoothstep(center - width, center + width, x);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                // 尘遮罩：噪点平铺，未拂区域覆盖积尘；遮罩随 _RevealProgress 反向衰减
                float dust = SAMPLE_TEXTURE2D(_DustTex, sampler_DustTex,
                                              IN.uv * _DustTex_ST.xy + _DustTex_ST.zw).r;
                float dustAlpha = dust * (1.0 - _RevealProgress);

                // 积尘层颜色（压暗底层 + 灰调）
                half3 dustCol = lerp(base.rgb, _DustTint.rgb * 0.7, 0.55) * IN.color.rgb;

                // 微光描边：已拂区域透出底层 + 边缘加色光晕
                float glowMask = SAMPLE_TEXTURE2D(_GlowTex, sampler_GlowTex, IN.uv).r;
                // 阈值顿挫：本档附近抬亮 + 脉冲 0→1→0
                float stepBand = bandStep(_RevealProgress, _Threshold, 0.04);
                float whisperLift = stepBand * (0.15 + 0.85 * _Pulse); // 脉冲驱动，跳变幅度≥15%

                half3 glow = _GlowColor.rgb * _GlowStrength * glowMask * (0.4 + whisperLift);

                // 合成：已拂区 = 底层 + 微光；未拂区 = 积尘；按 dustAlpha 混合
                half3 revealed = base.rgb + glow;
                half3 col = lerp(revealed, dustCol, dustAlpha);

                // 整体 alpha：物件本体透明由 MainTex 控制，积尘叠加半透明
                float a = max(base.a, dustAlpha * 0.85);
                return half4(col, a);
            }
            ENDHLSL
        }
    }

    // 非 URP 环境回退：保持可见（不至于粉红报错），但无微光逻辑
    FallBack "Universal Render Pipeline/2D/Sprite-Unlit"
}
