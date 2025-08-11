using System.Collections.Generic;
using UnityEngine;
using mj.gist;

namespace Chiyi
{
    [ExecuteInEditMode]
    public class EffectController : MonoBehaviour
    {
        [SerializeField] private EffectType _effectType;
        [SerializeField] private Texture2D _sourceTex;
        [SerializeField] private RenderTexture _output;

        [Header("Mask")]
        [SerializeField] private MaskSetting _maskSetting;

        [Header("Edge")]
        [SerializeField] private EdgeSetting _edgeSetting;

        [Header("Shift")]
        [SerializeField] private ShiftSetting _shiftSetting;

        [Header("Saturation Mask")]
        [SerializeField] private SaturationMaskSetting _saturationMaskSetting;

        [Header("Composite")]
        [SerializeField] private CompositeSetting _compositeSetting;


        void Update()
        {

            Shader.SetGlobalTexture("_SourceTex", _sourceTex);

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


            switch (_effectType){
                case EffectType.Mask:
                    Graphics.Blit(_sourceTex, _output, _maskSetting.mats[0]);
                    break;
                case EffectType.Edge:
                    Graphics.Blit(_sourceTex, _output, _edgeSetting.mat);
                    break;
                case EffectType.Shift:
                    Graphics.Blit(_sourceTex, _output, _shiftSetting.mat);
                    break;
                case EffectType.SaturationMask:
                    Graphics.Blit(_sourceTex, _output, _saturationMaskSetting.mat);
                    break;
                case EffectType.Composite:
                    Graphics.Blit(_sourceTex, _output, _compositeSetting.mat);
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

            public void Update(){
                foreach (var m in mats){
                    if (m == null) continue;
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

            public void Update(){
                if (mat == null) return;
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
            public Vector2 smoothRange;
            public BlurParams blurParams;


            public void Update(){
                if (mat == null) return;
                mat.SetVector("_SmoothRange", smoothRange);

                if (blurParams.filter == null) return;
                blurParams.filter.nIterations = blurParams.iterations;
                blurParams.filter.lod = blurParams.lod;
                blurParams.filter.step = blurParams.step;
            }
        }


        [System.Serializable]
        public class ShiftSetting
        {
            public Material mat;
            public float steps = 10;
            public float strength = 0.2f;
            public float maxSpan = 0.05f;

            public void Update(){
                if (mat == null) return;
                mat.SetFloat("_Steps", steps);
                mat.SetFloat("_Strength", strength);
                mat.SetFloat("_MaxSpan", maxSpan);
            }
        }

        [System.Serializable]
        public class CompositeSetting
        {
            public Material mat;
            public float glowStrength = 0.2f;
            [Range(0, 0.1f)] public float noiseOffset = 0.05f;

            public void Update(){
                if (mat == null) return;
                mat.SetFloat("_GlowStrength", glowStrength);
                mat.SetFloat("_NoiseOffset", noiseOffset);
            }
        }

        [System.Serializable]
        public class BlurParams{
            public GaussianFilter filter;
            public int iterations = 1;
            public int lod = 1;
            public float step = 1f;
        }

        public enum EffectType{
            Mask,
            Edge,
            Shift,
            SaturationMask,
            Composite
        }
    }
}