using System.Collections.Generic;
using UnityEngine;
using mj.gist;

namespace Chiyi
{
    [ExecuteInEditMode]
    public class EffectController : MonoBehaviour, ISource
    {
        [SerializeField] private EffectType _effectType;

        #region RenderTexture
        [SerializeField] private RenderTexture _mask;
        private RenderTexture mask
        {
            get
            {
                if (_mask == null)
                    _mask = CreateRenderTexture();
                return _mask;
            }
        }

        [SerializeField] private RenderTexture _maskBlur;
        private RenderTexture maskBlur
        {
            get
            {
                if (_maskBlur == null)
                    _maskBlur = CreateRenderTexture();
                return _maskBlur;
            }
        }

        [SerializeField] private RenderTexture _edge;
        private RenderTexture edge
        {
            get
            {
                if (_edge == null)
                    _edge = CreateRenderTexture();
                return _edge;
            }
        }

        [SerializeField] private RenderTexture _shift;
        private RenderTexture shift
        {
            get
            {
                if (_shift == null)
                    _shift = CreateRenderTexture();
                return _shift;
            }
        }

        [SerializeField] private RenderTexture _saturation;
        private RenderTexture saturation
        {
            get
            {
                if (_saturation == null)
                    _saturation = CreateRenderTexture();
                return _saturation;
            }
        }

        [SerializeField] private RenderTexture _saturationBlur;
        private RenderTexture saturationBlur
        {
            get
            {
                if (_saturationBlur == null)
                    _saturationBlur = CreateRenderTexture();
                return _saturationBlur;
            }
        }

        [SerializeField] private RenderTexture _composite;
        private RenderTexture composite
        {
            get
            {
                if (_composite == null)
                    _composite = CreateRenderTexture();
                return _composite;
            }
        }

        [SerializeField] private RenderTexture _output;

        #endregion

        [SerializeField] private Shader _blurShader;
        private Material blurMat
        {
            get
            {
                if (_blurMat == null)
                    _blurMat = new Material(_blurShader);
                return _blurMat;
            }
        }
        private Material _blurMat;

        private readonly int _width = 1920;
        private readonly int _height = 1080;

        [Header("Mask")]
        [SerializeField] private MaskSetting _maskSetting;
        [SerializeField] private BlurParams _maskBlurParams;

        [Header("Edge")]
        [SerializeField] private EdgeSetting _edgeSetting;

        [Header("Shift")]
        [SerializeField] private ShiftSetting _shiftSetting;

        [Header("Saturation Mask")]
        [SerializeField] private SaturationMaskSetting _saturationMaskSetting;
        [SerializeField] private BlurParams _saturationBlurParams;

        [Header("Composite")]
        [SerializeField] private CompositeSetting _compositeSetting;


        public Texture2D SourceTexture { get; set; }
        public float Ratio { get; set; }

        private RenderTexture CreateRenderTexture()
        {
            var rt = new RenderTexture(_width, _height, 0, RenderTextureFormat.ARGBFloat);
            rt.Create();
            return rt;
        }

        void Update()
        {
            // Shader.SetGlobalTexture("_SourceTex", SourceTexture);
            // Shader.SetGlobalFloat("_GlobalRatio", Ratio);

            // mask
            _maskSetting.Update(SourceTexture, mask);
            BlurUtil.BlurWithDownSample(mask, maskBlur, _maskBlurParams.lod, _maskBlurParams.iterations, _maskBlurParams.step, blurMat);

            // edge
            _edgeSetting.Update(SourceTexture, edge);

            // shift
            _shiftSetting.Update(SourceTexture, shift);

            // saturation mask
            _saturationMaskSetting.Update(SourceTexture, saturation);
            BlurUtil.BlurWithDownSample(saturation, saturationBlur, _saturationBlurParams.lod, _saturationBlurParams.iterations, _saturationBlurParams.step, blurMat);

            // composite
            _compositeSetting.mat.SetTexture("_SourceTex", SourceTexture);
            _compositeSetting.mat.SetTexture("_EdgeTex", edge);
            _compositeSetting.mat.SetTexture("_ShiftTex", shift);
            _compositeSetting.mat.SetTexture("_MaskTex", maskBlur);
            _compositeSetting.mat.SetTexture("_SaturationTex", saturationBlur);
            _compositeSetting.Update(SourceTexture, composite);

            switch (_effectType)
            {
                case EffectType.Original:
                    Graphics.Blit(SourceTexture, _output);
                    break;
                case EffectType.Mask:
                    Graphics.Blit(mask, _output);
                    break;
                case EffectType.MaskBlur:
                    Graphics.Blit(maskBlur, _output);
                    break;
                case EffectType.Edge:
                    Graphics.Blit(edge, _output);
                    break;
                case EffectType.Shift:
                    Graphics.Blit(shift, _output);
                    break;
                case EffectType.SaturationMask:
                    Graphics.Blit(saturation, _output);
                    break;
                case EffectType.SaturationMaskBlur:
                    Graphics.Blit(saturationBlur, _output);
                    break;
                case EffectType.Composite:
                    Graphics.Blit(composite, _output);
                    break;
            }
        }

        [ContextMenu("Save Texture")]
        public void SaveTexture()
        {
            TextureIO.SaveRenderTextureToPNG(_output, "Assets/Resource/prev.png");
        }

        [System.Serializable]
        public class MaskSetting
        {
            public Material mat;

            public Vector3 targetHSV;
            public Vector3 filterRange;
            public Vector2 smoothRange;

            public void Update(Texture source, RenderTexture target)
            {
                if (mat == null)
                    return;
                mat.SetVector("_TargetHSV", targetHSV);
                mat.SetVector("_FilterRange", filterRange);
                mat.SetVector("_SmoothRange", smoothRange);
                mat.SetTexture("_SourceTex", source);

                Graphics.Blit(source, target, mat, 0);
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

            public void Update(Texture source, RenderTexture target)
            {
                if (mat == null)
                    return;
                mat.SetFloat("_Threshold", threshold);
                mat.SetFloat("_Softness", softness);
                mat.SetFloat("_Gain", gain);
                mat.SetFloat("_Strength", strength);
                mat.SetTexture("_SourceTex", source);

                Graphics.Blit(source, target, mat, 0);
            }
        }

        [System.Serializable]
        public class ShiftSetting
        {
            public Material mat;
            public ShiftParams shiftParams1;
            public ShiftParams shiftParams2;

            public void Update(Texture source, RenderTexture target)
            {
                if (mat == null)
                    return;
                shiftParams1.Update(mat, 1);
                shiftParams2.Update(mat, 2);
                mat.SetTexture("_SourceTex", source);

                Graphics.Blit(source, target, mat, 0);
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
        public class SaturationMaskSetting
        {
            public Material mat;
            public float strength = 0.2f;
            public Vector2 smoothRange;
            public void Update(Texture source, RenderTexture target)
            {
                if (mat == null)
                    return;
                mat.SetFloat("_Strength", strength);
                mat.SetVector("_SmoothRange", smoothRange);
                mat.SetTexture("_SourceTex", source);

                Graphics.Blit(source, target, mat, 0);
            }
        }


        [System.Serializable]
        public class CompositeSetting
        {
            public Material mat;

            [Range(0, 0.1f)]
            public float noiseOffset = 0.05f;
            public Vector4 fbmParams = new Vector4(1, 1, 1, 1);

            public void Update(Texture source, RenderTexture target)
            {
                if (mat == null)
                    return;
                mat.SetFloat("_NoiseOffset", noiseOffset);
                mat.SetVector("_FbmParams", fbmParams);

                Graphics.Blit(source, target, mat, 0);
            }
        }

        [System.Serializable]
        public class BlurParams
        {
            public int iterations = 1;
            public int lod = 1;
            public float step = 1f;
        }

        public enum EffectType
        {
            Original,
            Mask,
            MaskBlur,
            Edge,
            Shift,
            SaturationMask,
            SaturationMaskBlur,
            Composite,
        }
    }
}
