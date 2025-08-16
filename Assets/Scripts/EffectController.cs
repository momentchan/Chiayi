using System.Collections.Generic;
using mj.gist;
using UnityEngine;

namespace Chiyi
{
    [ExecuteInEditMode]
    public class EffectController : MonoBehaviour, ISource
    {
        [SerializeField]
        private EffectType _effectType;

        [SerializeField]
        private RenderTexture _output;

        [Header("Mask")]
        [SerializeField]
        private MaskSetting _maskSetting;

        [Header("Edge")]
        [SerializeField]
        private EdgeSetting _edgeSetting;

        [Header("Shift")]
        [SerializeField]
        private ShiftSetting _shiftSetting;

        [Header("Saturation Mask")]
        [SerializeField]
        private SaturationMaskSetting _saturationMaskSetting;

        [Header("Composite")]
        [SerializeField]
        private CompositeSetting _compositeSetting;

        public Texture2D SourceTexture { get; set; }

        public float Ratio { get; set; }

        void Update()
        {
            Shader.SetGlobalTexture("_SourceTex", SourceTexture);
            Shader.SetGlobalFloat("_GlobalRatio", Ratio);

            // mask
            _maskSetting.Update();

            // edge
            _edgeSetting.Update();

            // shift
            _shiftSetting.Update();

            // saturation mask
            _saturationMaskSetting.Update();

            // composite
            _compositeSetting.Update();

            switch (_effectType)
            {
                case EffectType.Original:
                    Graphics.Blit(SourceTexture, _output);
                    break;
                case EffectType.Mask:
                    Graphics.Blit(SourceTexture, _output, _maskSetting.mats[0]);
                    break;
                case EffectType.Edge:
                    Graphics.Blit(SourceTexture, _output, _edgeSetting.mat);
                    break;
                case EffectType.Shift:
                    Graphics.Blit(SourceTexture, _output, _shiftSetting.mat);
                    break;
                case EffectType.SaturationMask:
                    Graphics.Blit(SourceTexture, _output, _saturationMaskSetting.mat);
                    break;
                case EffectType.Composite:
                    Graphics.Blit(SourceTexture, _output, _compositeSetting.mat);
                    break;
            }
        }

        [System.Serializable]
        public class MaskSetting
        {
            public List<Material> mats;
            public Vector3 targetHSV;
            public Vector3 filterRange;
            public Vector2 smoothRange;

            public void Update()
            {
                foreach (var m in mats)
                {
                    if (m == null)
                        continue;
                    m.SetVector("_TargetHSV", targetHSV);
                    m.SetVector("_FilterRange", filterRange);
                    m.SetVector("_SmoothRange", smoothRange);
                }
            }
        }

        [System.Serializable]
        public class EdgeSetting
        {
            public Material mat;
            public float threshold = 0.3f;
            public float softness = 0.03f;
            public float gain = 0.98f;
            public float strength = 1f;

            public void Update()
            {
                if (mat == null)
                    return;
                mat.SetFloat("_Threshold", threshold);
                mat.SetFloat("_Softness", softness);
                mat.SetFloat("_Gain", gain);
                mat.SetFloat("_Strength", strength);
            }
        }

        [System.Serializable]
        public class SaturationMaskSetting
        {
            public Material mat;
            public float strength = 0.2f;
            public Vector2 smoothRange;
            public BlurParams blurParams;

            public void Update()
            {
                if (mat == null)
                    return;
                mat.SetFloat("_Strength", strength);
                mat.SetVector("_SmoothRange", smoothRange);

                if (blurParams.filter == null)
                    return;
                blurParams.filter.nIterations = blurParams.iterations;
                blurParams.filter.lod = blurParams.lod;
                blurParams.filter.step = blurParams.step;
            }
        }

        [System.Serializable]
        public class ShiftSetting
        {
            public Material mat;
            public ShiftParams shiftParams1;
            public ShiftParams shiftParams2;

            public void Update()
            {
                if (mat == null)
                    return;
                shiftParams1.Update(mat, 1);
                shiftParams2.Update(mat, 2);
            }

            [System.Serializable]
            public class ShiftParams
            {
                public bool enable;
                public float steps;
                public float maxSpan;

                [Range(0, 1f)]
                public float randomness;

                [Range(0, 1f)]
                public float strength;

                [Range(0, 1f)]
                public float sigma;
                public Vector2 wave;

                public void Update(Material mat, int index)
                {
                    mat.SetFloat($"_Steps{index}", steps);
                    mat.SetFloat($"_Strength{index}", enable ? strength : 0);
                    mat.SetFloat($"_MaxSpan{index}", maxSpan);
                    mat.SetFloat($"_Sigma{index}", sigma);
                    mat.SetFloat($"_Randomness{index}", randomness);
                    mat.SetVector($"_Wave{index}", wave);
                }
            }
        }

        [System.Serializable]
        public class CompositeSetting
        {
            public Material mat;

            [Range(0, 0.1f)]
            public float noiseOffset = 0.05f;
            public Vector4 fbmParams = new Vector4(1, 1, 1, 1);

            public void Update()
            {
                if (mat == null)
                    return;
                mat.SetFloat("_NoiseOffset", noiseOffset);
                mat.SetVector("_FbmParams", fbmParams);
            }
        }

        [System.Serializable]
        public class BlurParams
        {
            public GaussianFilter filter;
            public int iterations = 1;
            public int lod = 1;
            public float step = 1f;
        }

        public enum EffectType
        {
            Original,
            Mask,
            Edge,
            Shift,
            SaturationMask,
            Composite,
        }
    }
}
