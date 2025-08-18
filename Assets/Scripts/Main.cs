using System;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using Osc;
using UnityEngine;

namespace Chiyi
{
    [ExecuteInEditMode]
    public class Main : MonoBehaviour
    {
        [Header("Output / Blend Material")]
        [SerializeField] private Material _outputMat;

        [Header("Optional Capture")]
        [SerializeField] private RenderTexture _spoutTex;
        [SerializeField] private string _outputFolder = "C:/Chiayi/";

        [Header("Blend Controls")]
        [SerializeField, Range(0f, 1f)] private float _prevFloor = 0.2f; // final blend of Current
        [SerializeField, Min(0f)] private float _fadeDuration = 0.8f;    // fade duration
        [SerializeField] private AnimationCurve _ease = null; // 可為 null=線性


        [Header("Internal (do not edit at runtime)")]
        [SerializeField] private int _currentIndex = 0;                  // index of Current
        [SerializeField] private List<EffectInstance> _effects = new List<EffectInstance>(); // 3 slots: Prev/Current/Next

        private Coroutine _blendCo;

        private int WrapIndex(int i)
        {
            int n = Mathf.Max(1, _effects.Count);
            return (i % n + n) % n; // ensure non-negative
        }

        private EffectInstance Prev => _effects.Count >= 3 ? _effects[WrapIndex(_currentIndex - 1)] : null;
        private EffectInstance Current => _effects.Count >= 3 ? _effects[WrapIndex(_currentIndex)] : null;
        private EffectInstance Next => _effects.Count >= 3 ? _effects[WrapIndex(_currentIndex + 1)] : null;


        public void OnReceivePath(OscPort.Capsule c)
        {
            try
            {
                var msg = c.message;
                var path = (string)msg.data[0];

                TextureIO.LoadTextureFromFile(path, (tex) =>
                {
                    Debug.Log("Loaded texture: " + path);
                    BeginSwitch(tex);
                });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void BeginSwitch(Texture2D newTex)
        {
            var next = Next;
            if (next == null)
            {
                Debug.LogWarning("Next slot is null.");
                return;
            }

            next.source = newTex;
            if (next.controller != null)
            {
                next.controller.Source = newTex;
            }

            if (_blendCo != null) StopCoroutine(_blendCo);
            _blendCo = StartCoroutine(BlendEffect());
        }

        //  coroutine: Prev → 0、Current → _prevFloor、Next → 1（smooth transition）

        private IEnumerator BlendEffect()
        {
            // —— 1) 準備 Next，強制乾淨起點 —— //
            if (Next != null)
            {
                // 綁定新來源（BeginSwitch 時已經設好 source）
                if (Next.controller != null)
                {
                    Next.controller.Source = Next.source;
                    Next.controller.Ratio = 0f;   // 內部效果從 0 開始
                }

                Next.ratio = 0f;  // 外部合成：Next 的 ratio 從 0 開始
                Next.blend = 1f;  // 外部合成：Next 立即全開（保持最大）
            }

            // 讓 Controller 有機會產出有效的 Output（避免第一幀黑圖/錯幀）
            yield return null;                // 等一幀
                                              // 也可更保險：yield return new WaitForEndOfFrame();

            // —— 2) 讀取當下值作為「平滑銜接」起點 —— //
            float startPrevBlend = Prev?.blend ?? 0f;
            float startCurrentBlend = Current?.blend ?? 0f;
            float startNextRatio = Next?.ratio ?? 0f; // 理論上是 0

            // 目標
            const float targetPrevBlend = 0f;
            float targetCurrentBlend = _prevFloor;
            const float targetNextRatio = 1f;

            // —— 3) 漸變 —— //
            float t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, _fadeDuration));
                if (_ease != null) k = _ease.Evaluate(k);

                // Prev: blend → 0
                if (Prev != null)
                    Prev.blend = Mathf.Lerp(startPrevBlend, targetPrevBlend, k);

                // Current: blend → prevFloor
                if (Current != null)
                    Current.blend = Mathf.Lerp(startCurrentBlend, targetCurrentBlend, k);

                // Next: ratio 0→1（blend 保持 1 不動）
                if (Next != null)
                    Next.ratio = Mathf.Lerp(startNextRatio, targetNextRatio, k);

                // 可選：也把內部 Controller.Ratio 同步推（若你想連內部特效強度也一起變）
                // if (Next?.controller != null) Next.controller.Ratio = Next.ratio;

                yield return null;
            }

            // —— 4) 收尾鎖定 —— //
            if (Prev != null) Prev.blend = targetPrevBlend;
            if (Current != null) Current.blend = targetCurrentBlend;
            if (Next != null) Next.ratio = targetNextRatio;

            // —— 5) 輪轉：Next → Current —— //
            _currentIndex = WrapIndex(_currentIndex + 1);
            _blendCo = null;
        }

        private Texture SafeTex(EffectInstance inst)
        {
            if (inst != null && inst.controller != null && inst.controller.Output != null)
                return inst.controller.Output;
            return Texture2D.blackTexture;
        }

        // get safe blend value (0 if null)
        private float SafeBlend(EffectInstance inst, float fallback = 0f)
        {
            return inst != null ? inst.blend : fallback;
        }

        // update every frame
        private void Update()
        {
            if (_effects == null || _effects.Count < 3 || _outputMat == null) return;

            // update EffectController
            Prev?.Update();
            Current?.Update();
            Next?.Update();

            // set texture
            _outputMat.SetTexture("_Prev", SafeTex(Prev));
            _outputMat.SetTexture("_Current", SafeTex(Current));
            _outputMat.SetTexture("_Next", SafeTex(Next));

            _outputMat.SetFloat("_PrevBlend", SafeBlend(Prev));
            _outputMat.SetFloat("_CurrentBlend", SafeBlend(Current));
            _outputMat.SetFloat("_NextBlend", SafeBlend(Next));

            // test key: press T to trigger Blend (no new texture, just rotate blend)
            if (Input.GetKeyDown(KeyCode.T))
            {
                if (_blendCo != null) StopCoroutine(_blendCo);
                _blendCo = StartCoroutine(BlendEffect());
            }
        }

        // optional: save current Spout RT
        private IEnumerator SaveTexture()
        {
            yield return new WaitForSeconds(1);
            if (_spoutTex == null) yield break;

            var timestamp = DateTime.Now.ToString("yyMMdd_HHmmss");
            var filename = $"output_{timestamp}.png";
            var fullPath = Path.Combine(_outputFolder, filename);

            TextureIO.SaveRenderTextureToPNG(_spoutTex, fullPath);
            Debug.Log($"Saved: {fullPath}");
        }

        private void OnDestroy()
        {
            if (_blendCo != null) { StopCoroutine(_blendCo); _blendCo = null; }
        }
    }

    [Serializable]
    public class EffectInstance
    {
        public EffectController controller;
        public Texture2D source;
        [Range(0f, 1f)] public float ratio; // internal effect
        [Range(0f, 1f)] public float blend; // external blend
        public Color bgColor;

        public void Update()
        {
            if (controller == null) return;
            controller.Source = source;
            controller.Ratio = ratio;
            controller.BgColor = bgColor;
        }
    }
}
