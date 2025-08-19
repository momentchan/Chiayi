using UnityEngine;
using UnityEngine.VFX;

namespace Chiayi
{
    /// <summary>
    /// Extracts pixel points from a texture and feeds them to a Visual Effect Graph
    /// for particle generation based on pixel brightness threshold
    /// </summary>
    public class PixelExtractor : MonoBehaviour
    {
        [Header("VFX Configuration")]
        [SerializeField] private VisualEffect _visualEffect;
        [SerializeField] private ComputeShader _extractionShader;

        [SerializeField] private TextureToNormal _textureToNormal;
        
        [Header("Brightness Extraction Settings")]
        [SerializeField, Range(0f, 1f)] private float _brightnessThreshold = 0.1f;
        [SerializeField] private bool _usePerceptualBrightness = true;
        

        #region Private Fields and Properties
        
        // Graphics buffers for compute shader
        private GraphicsBuffer _pointBuffer;
        private GraphicsBuffer _countBuffer;
        
        // Current effect instance being processed
        private EffectInstance _effect;
        
        /// <summary>
        /// Get the active source texture (either from IEffect.Source or _sourceTexture)
        /// </summary>
        private Texture ActiveSourceTexture => _effect.controller.Output;
        
        /// <summary>
        /// Get buffer for extracted points (lazy initialization)
        /// </summary>
        private GraphicsBuffer PointBuffer
        {
            get
            {
                if (_pointBuffer == null && ActiveSourceTexture != null)
                {
                    InitializeBuffers();
                }
                return _pointBuffer;
            }
        }
        
        /// <summary>
        /// Get buffer for point count (lazy initialization)
        /// </summary>
        private GraphicsBuffer CountBuffer
        {
            get
            {
                if (_countBuffer == null && ActiveSourceTexture != null)
                {
                    InitializeBuffers();
                }
                return _countBuffer;
            }
        }
        
        /// <summary>
        /// Get maximum number of points based on texture size
        /// </summary>
        private int MaxPoints
        {
            get
            {
                var texture = ActiveSourceTexture;
                return texture != null ? texture.width * texture.height : 0;
            }
        }
        
        #endregion

        #region Unity Lifecycle
        
        void OnDisable()
        {
            CleanupBuffers();
        }
        
        void OnDestroy()
        {
            CleanupBuffers();
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Execute pixel extraction for a specific effect instance
        /// </summary>
        public void Execute(EffectInstance effectInstance)
        {
            if (effectInstance?.controller?.Output != null)
            {
                _effect = effectInstance;
                _textureToNormal.SetSourceAndGenerate(_effect.controller.SaturationBlur);
                
                ExtractPoints();
            }
            else
            {
                Debug.LogWarning("PixelExtractor: Invalid effect instance provided for execution", this);
            }
        }
        
        /// <summary>
        /// Extract points from the current source texture
        /// </summary>
        [ContextMenu("Extract Points")]
        public void ExtractPoints()
        {
            if (!ValidateSetup())
                return;
                
            try
            {
                PerformExtraction();
                UpdateVisualEffect();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"PixelExtractor: Error during extraction - {ex.Message}", this);
            }
        }
        
        /// <summary>
        /// Validate and log the current setup status
        /// </summary>
        [ContextMenu("Validate Setup")]
        public void LogValidationStatus()
        {
            var issues = new System.Collections.Generic.List<string>();
            
            if (_extractionShader == null)
                issues.Add("Extraction compute shader is not assigned");
                
            if (_visualEffect == null)
                issues.Add("Visual Effect is not assigned");
                
            if (ActiveSourceTexture == null)
                issues.Add("No source texture available");
                
            if (issues.Count > 0)
            {
                Debug.LogWarning($"PixelExtractor validation issues:\n- {string.Join("\n- ", issues)}", this);
            }
            else
            {
                Debug.Log("PixelExtractor setup is valid!", this);
            }
        }
        
        #endregion
        
        #region Private Methods
        
        /// <summary>
        /// Validate that all required components are set up
        /// </summary>
        private bool ValidateSetup()
        {
            if (_extractionShader == null)
            {
                Debug.LogWarning("PixelExtractor: No extraction shader assigned", this);
                return false;
            }
            
            if (_visualEffect == null)
            {
                Debug.LogWarning("PixelExtractor: No visual effect assigned", this);
                return false;
            }
            
            if (ActiveSourceTexture == null)
            {
                Debug.LogWarning("PixelExtractor: No source texture available", this);
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Initialize graphics buffers based on current texture size
        /// </summary>
        private void InitializeBuffers()
        {
            var texture = ActiveSourceTexture;
            if (texture == null) return;
            
            CleanupBuffers();
            
            try
            {
                // Create point buffer for UV coordinates
                _pointBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Append,
                    MaxPoints,
                    sizeof(float) * 2 // UV coordinates (2 floats)
                );
                _pointBuffer.SetCounterValue(0);
                
                // Create count buffer
                _countBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Raw,
                    1,
                    sizeof(uint)
                );
                

                Debug.Log($"PixelExtractor: Initialized buffers for {texture.width}x{texture.height} texture", this);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"PixelExtractor: Failed to initialize buffers - {ex.Message}", this);
                CleanupBuffers();
            }
        }
        
        /// <summary>
        /// Perform the actual point extraction using compute shader
        /// </summary>
        private void PerformExtraction()
        {
            var texture = ActiveSourceTexture;
            
            // Reset point buffer counter
            PointBuffer.SetCounterValue(0);
            
            // Set compute shader parameters
            _extractionShader.SetTexture(0, "_Tex", texture);
            _extractionShader.SetInts("_Size", texture.width, texture.height);
            _extractionShader.SetFloat("_Epsilon", _brightnessThreshold);
            _extractionShader.SetBool("_UsePerceptualBrightness", _usePerceptualBrightness);
            _extractionShader.SetBuffer(0, "_OutUV", PointBuffer);
            
            // Dispatch compute shader
            int threadGroupsX = (texture.width + 7) / 8;
            int threadGroupsY = (texture.height + 7) / 8;
            _extractionShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
        }
        
        /// <summary>
        /// Update the visual effect with extracted points
        /// </summary>
        private void UpdateVisualEffect()
        {
            // Get point count
            GraphicsBuffer.CopyCount(PointBuffer, CountBuffer, 0);
            uint[] countArray = new uint[1];
            CountBuffer.GetData(countArray);
            uint pointCount = countArray[0];
            
            // Update visual effect
            _visualEffect.SetGraphicsBuffer("SamplesUV", PointBuffer);
            _visualEffect.SetUInt("SamplesCount", pointCount);
            _visualEffect.SetTexture("SourceTex", ActiveSourceTexture);
            _visualEffect.SetTexture("NormalTex", _textureToNormal.normalTexture);
            
            string brightnessType = _usePerceptualBrightness ? "perceptual" : "maximum RGB";
            Debug.Log($"PixelExtractor: Extracted {pointCount} points using {brightnessType} brightness (threshold: {_brightnessThreshold:F3}) from {ActiveSourceTexture.width}x{ActiveSourceTexture.height} texture", this);
        }
        
        /// <summary>
        /// Clean up graphics buffers
        /// </summary>
        private void CleanupBuffers()
        {
            _pointBuffer?.Dispose();
            _pointBuffer = null;
            
            _countBuffer?.Dispose();
            _countBuffer = null;
        }
        
        #endregion
    }
}
