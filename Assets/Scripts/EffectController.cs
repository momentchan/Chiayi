using System.Collections.Generic;
using UnityEngine;
using mj.gist;

namespace Chiyi
{
    [ExecuteInEditMode]
    public class EffectController : MonoBehaviour, IEffect
    {
        [Header("Configuration")]
        [SerializeField] private EffectType _effectType;
        [SerializeField] private Vector2Int _textureSize = new Vector2Int(1920, 1080);
        [SerializeField] private RenderTextureFormat _textureFormat = RenderTextureFormat.ARGBFloat;

        #region RenderTextures
        // Cached render textures - lazily created
        private readonly Dictionary<string, RenderTexture> _renderTextures = new Dictionary<string, RenderTexture>();

        // Texture accessors with lazy creation
        private RenderTexture output => GetOrCreateRenderTexture("output");
        private RenderTexture mask => GetOrCreateRenderTexture("mask");
        private RenderTexture maskBlur => GetOrCreateRenderTexture("maskBlur");
        private RenderTexture edge => GetOrCreateRenderTexture("edge");
        private RenderTexture shift => GetOrCreateRenderTexture("shift");
        private RenderTexture saturation => GetOrCreateRenderTexture("saturation");
        private RenderTexture saturationBlur => GetOrCreateRenderTexture("saturationBlur");
        private RenderTexture composite => GetOrCreateRenderTexture("composite");
        #endregion

        [Header("Shaders")]
        [SerializeField] private Shader _blurShader;
        private Material _blurMat;
        private Material blurMat
        {
            get
            {
                if (_blurMat == null && _blurShader != null)
                    _blurMat = new Material(_blurShader);
                return _blurMat;
            }
        }

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


        public Texture2D Source { get; set; }
        public RenderTexture Output { get => output; }
        public float Ratio { get; set; }

        /// <summary>
        /// Get or create a render texture with the specified name
        /// </summary>
        private RenderTexture GetOrCreateRenderTexture(string name)
        {
            if (_renderTextures.TryGetValue(name, out RenderTexture rt))
            {
                if (rt != null && rt.IsCreated())
                    return rt;
            }

            rt = CreateRenderTexture();
            rt.name = $"EffectController_{name}";
            _renderTextures[name] = rt;
            return rt;
        }

        /// <summary>
        /// Create a new render texture with current settings
        /// </summary>
        private RenderTexture CreateRenderTexture()
        {
            var rt = new RenderTexture(_textureSize.x, _textureSize.y, 0, _textureFormat)
            {
                enableRandomWrite = false,
                useMipMap = false,
                autoGenerateMips = false
            };
            rt.Create();
            return rt;
        }


        void OnEnable()
        {
            CreateInstanceMaterials();
        }

        void Update()
        {
            if (Source == null)
                return;

            try
            {
                ProcessEffects();
                GenerateOutput();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"EffectController: Error processing effects - {ex.Message}", this);
            }
        }

        /// <summary>
        /// Process all visual effects
        /// </summary>
        private void ProcessEffects()
        {
            if (Source == null)
            {
                Debug.LogWarning("EffectController: Source texture is null", this);
                return;
            }

            // Set global ratio to all materials
            SetRatioToAllMaterials();

            // Process mask effect
            ProcessMaskEffect();

            // Process edge effect
            ProcessEdgeEffect();

            // Process shift effect
            ProcessShiftEffect();

            // Process saturation mask effect
            ProcessSaturationMaskEffect();

            // Process composite effect
            ProcessCompositeEffect();
        }

        /// <summary>
        /// Set the ratio value to all effect materials
        /// </summary>
        private void SetRatioToAllMaterials()
        {
            if (_maskSetting?.mat != null)
                _maskSetting.mat.SetFloat("_Ratio", Ratio);
                
            if (_edgeSetting?.mat != null)
                _edgeSetting.mat.SetFloat("_Ratio", Ratio);
                
            if (_shiftSetting?.mat != null)
                _shiftSetting.mat.SetFloat("_Ratio", Ratio);
                
            if (_saturationMaskSetting?.mat != null)
                _saturationMaskSetting.mat.SetFloat("_Ratio", Ratio);
                
            if (_compositeSetting?.mat != null)
                _compositeSetting.mat.SetFloat("_Ratio", Ratio);
        }

        private void ProcessMaskEffect()
        {
            if (_maskSetting?.mat != null)
            {
                _maskSetting.Update(Source, mask);

                // Apply blur if blur material is available
                if (blurMat != null)
                {
                    BlurUtil.BlurWithDownSample(mask, maskBlur,
                        _maskBlurParams.lod, _maskBlurParams.iterations, _maskBlurParams.step, blurMat);
                }
                else
                {
                    Graphics.Blit(mask, maskBlur); // Fallback without blur
                }
            }
        }

        private void ProcessEdgeEffect()
        {
            if (_edgeSetting?.mat != null)
            {
                _edgeSetting.Update(Source, edge);
            }
        }

        private void ProcessShiftEffect()
        {
            if (_shiftSetting?.mat != null)
            {
                _shiftSetting.Update(Source, shift);
            }
        }

        private void ProcessSaturationMaskEffect()
        {
            if (_saturationMaskSetting?.mat != null)
            {
                _saturationMaskSetting.Update(Source, saturation);

                // Apply blur if blur material is available
                if (blurMat != null)
                {
                    BlurUtil.BlurWithDownSample(saturation, saturationBlur,
                        _saturationBlurParams.lod, _saturationBlurParams.iterations, _saturationBlurParams.step, blurMat);
                }
                else
                {
                    Graphics.Blit(saturation, saturationBlur); // Fallback without blur
                }
            }
        }

        private void ProcessCompositeEffect()
        {
            if (_compositeSetting?.mat != null)
            {
                // Set all input textures for composite
                _compositeSetting.mat.SetTexture("_SourceTex", Source);
                _compositeSetting.mat.SetTexture("_EdgeTex", edge);
                _compositeSetting.mat.SetTexture("_ShiftTex", shift);
                _compositeSetting.mat.SetTexture("_MaskTex", maskBlur);
                _compositeSetting.mat.SetTexture("_SaturationTex", saturationBlur);

                _compositeSetting.Update(Source, composite);
            }
        }

        /// <summary>
        /// Generate the final output based on selected effect type
        /// </summary>
        private void GenerateOutput()
        {
            if (output == null)
            {
                Debug.LogWarning("EffectController: Output render texture is not assigned", this);
                return;
            }

            Texture sourceTexture = GetEffectTexture(_effectType);
            if (sourceTexture != null)
            {
                Graphics.Blit(sourceTexture, output);
            }
            else
            {
                // Fallback to original texture
                Graphics.Blit(Source, output);
            }
        }

        /// <summary>
        /// Get the appropriate texture for the specified effect type
        /// </summary>
        private Texture GetEffectTexture(EffectType effectType)
        {
            return effectType switch
            {
                EffectType.Original => Source,
                EffectType.Mask => mask,
                EffectType.MaskBlur => maskBlur,
                EffectType.Edge => edge,
                EffectType.Shift => shift,
                EffectType.SaturationMask => saturation,
                EffectType.SaturationMaskBlur => saturationBlur,
                EffectType.Composite => composite,
                _ => Source
            };
        }

        [ContextMenu("Save Texture")]
        public void SaveTexture()
        {
            if (output != null)
            {
                string timestamp = System.DateTime.Now.ToString("yyMMdd_HHmmss");
                string path = $"Assets/Resource/output_{timestamp}.png";
                TextureIO.SaveRenderTextureToPNG(output, path);
                Debug.Log($"Texture saved to: {path}", this);
            }
            else
            {
                Debug.LogWarning("EffectController: No output texture to save", this);
            }
        }

        public void CreateInstanceMaterials()
        {
            // Create instance materials to avoid sharing between controllers
            if (_maskSetting?.mat != null)
                _maskSetting.mat = new Material(_maskSetting.mat);

            if (_edgeSetting?.mat != null)
                _edgeSetting.mat = new Material(_edgeSetting.mat);

            if (_shiftSetting?.mat != null)
                _shiftSetting.mat = new Material(_shiftSetting.mat);

            if (_saturationMaskSetting?.mat != null)
                _saturationMaskSetting.mat = new Material(_saturationMaskSetting.mat);

            if (_compositeSetting?.mat != null)
                _compositeSetting.mat = new Material(_compositeSetting.mat);

            Debug.Log("Instance materials created - this controller now has unique materials", this);
        }

        void OnDestroy()
        {
            CleanupResources();
        }

        void OnDisable()
        {
            CleanupResources();
        }

        /// <summary>
        /// Clean up render textures and materials
        /// </summary>
        private void CleanupResources()
        {
            // Cleanup render textures
            foreach (var kvp in _renderTextures)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.Release();
                    DestroyImmediate(kvp.Value);
                }
            }
            _renderTextures.Clear();

            // Cleanup blur material
            if (_blurMat != null)
            {
                DestroyImmediate(_blurMat);
                _blurMat = null;
            }
        }

        [System.Serializable]
        public class MaskSetting
        {
            [Header("Material")]
            public Material mat;

            [Header("HSV Filtering")]
            public Vector3 targetHSV;
            public Vector3 filterRange;
            public Vector2 smoothRange;

            public void Update(Texture source, RenderTexture target, float ratio = 1f)
            {
                if (mat == null || source == null || target == null)
                {
                    if (mat == null) Debug.LogWarning("MaskSetting: Material is null");
                    return;
                }

                mat.SetVector("_TargetHSV", targetHSV);
                mat.SetVector("_FilterRange", filterRange);
                mat.SetVector("_SmoothRange", smoothRange);
                mat.SetTexture("_SourceTex", source);
                mat.SetFloat("_Ratio", ratio);

                Graphics.Blit(source, target, mat, 0);
            }
        }

        [System.Serializable]
        public class EdgeSetting
        {
            [Header("Material")]
            public Material mat;

            [Header("Edge Detection Parameters")]
            [Range(0f, 1f)] public float threshold = 0.3f;
            [Range(0f, 0.1f)] public float softness = 0.03f;
            [Range(0f, 1f)] public float gain = 0.98f;
            [Range(0f, 2f)] public float strength = 1f;

            public void Update(Texture source, RenderTexture target)
            {
                if (mat == null || source == null || target == null)
                {
                    if (mat == null) Debug.LogWarning("EdgeSetting: Material is null");
                    return;
                }

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
            [Header("Material")]
            public Material mat;

            [Header("Shift Parameters")]
            public ShiftParams shiftParams1 = new ShiftParams();
            public ShiftParams shiftParams2 = new ShiftParams();

            public void Update(Texture source, RenderTexture target)
            {
                if (mat == null || source == null || target == null)
                {
                    if (mat == null) Debug.LogWarning("ShiftSetting: Material is null");
                    return;
                }

                shiftParams1.Update(mat, 1);
                shiftParams2.Update(mat, 2);
                mat.SetTexture("_SourceTex", source);

                Graphics.Blit(source, target, mat, 0);
            }

            [System.Serializable]
            public class ShiftParams
            {
                [Header("Shift Control")]
                public bool enable = false;

                [Header("Shift Values")]
                [Range(1f, 32f)] public float steps = 8f;
                [Range(0f, 100f)] public float maxSpan = 10f;
                [Range(0f, 1f)] public float randomness = 0.5f;
                [Range(0f, 1f)] public float strength = 0.5f;
                [Range(0f, 1f)] public float sigma = 0.5f;
                public Vector2 wave = Vector2.one;

                public void Update(Material mat, int index)
                {
                    if (mat == null) return;

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
            [Header("Material")]
            public Material mat;

            [Header("Saturation Parameters")]
            [Range(0f, 1f)] public float strength = 0.2f;
            public Vector2 smoothRange;

            public void Update(Texture source, RenderTexture target)
            {
                if (mat == null || source == null || target == null)
                {
                    if (mat == null) Debug.LogWarning("SaturationMaskSetting: Material is null");
                    return;
                }

                mat.SetFloat("_Strength", strength);
                mat.SetVector("_SmoothRange", smoothRange);
                mat.SetTexture("_SourceTex", source);

                Graphics.Blit(source, target, mat, 0);
            }
        }


        [System.Serializable]
        public class CompositeSetting
        {
            [Header("Material")]
            public Material mat;

            [Header("Composite Parameters")]
            [Range(0f, 0.1f)] public float noiseOffset = 0.05f;
            public Vector4 fbmParams = new Vector4(1, 1, 1, 1);

            public void Update(Texture source, RenderTexture target)
            {
                if (mat == null || source == null || target == null)
                {
                    if (mat == null) Debug.LogWarning("CompositeSetting: Material is null");
                    return;
                }

                mat.SetFloat("_NoiseOffset", noiseOffset);
                mat.SetVector("_FbmParams", fbmParams);

                Graphics.Blit(source, target, mat, 0);
            }
        }

        [System.Serializable]
        public class BlurParams
        {
            [Header("Blur Parameters")]
            [Range(1, 8)] public int iterations = 1;
            [Range(0, 4)] public int lod = 1;
            [Range(0.1f, 4f)] public float step = 1f;
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
