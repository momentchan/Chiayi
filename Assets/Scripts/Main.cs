using System;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using Osc;
using UnityEngine;

namespace Chiayi
{
    [ExecuteInEditMode]
    public class Main : MonoBehaviour
    {
        [Header("Output Configuration")]
        [SerializeField] private Material _outputMaterial;
        [SerializeField] private RenderTexture _captureTexture;
        [SerializeField] private OscPort _osc;
        [SerializeField] private string _outputFolder = "C:/Chiayi/";

        [Header("Transition Settings")]
        [SerializeField, Range(0f, 1f)] private float _previousLayerOpacity = 0.2f;
        [SerializeField, Min(0f)] private float _transitionDuration = 0.8f;
        [SerializeField] private AnimationCurve _transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Effect Instances")]
        [SerializeField] private List<EffectInstance> _effectInstances = new List<EffectInstance>();

        [SerializeField] private PixelExtractor _pixelExtractor;
        
        // State management
        private int _currentEffectIndex = 0;
        private Coroutine _transitionCoroutine;
        
        // Constants
        private const int REQUIRED_EFFECT_COUNT = 3;

        #region Effect Instance Management
        
        /// <summary>
        /// Safely wrap index within effect instances bounds
        /// </summary>
        private int WrapIndex(int index)
        {
            if (_effectInstances.Count == 0) return 0;
            return ((index % _effectInstances.Count) + _effectInstances.Count) % _effectInstances.Count;
        }
        
        /// <summary>
        /// Get the previous effect instance in the sequence
        /// </summary>
        private EffectInstance PreviousEffect => IsValidEffectSetup() ? _effectInstances[WrapIndex(_currentEffectIndex - 1)] : null;
        
        /// <summary>
        /// Get the current active effect instance
        /// </summary>
        private EffectInstance CurrentEffect => IsValidEffectSetup() ? _effectInstances[WrapIndex(_currentEffectIndex)] : null;
        
        /// <summary>
        /// Get the next effect instance in the sequence
        /// </summary>
        private EffectInstance NextEffect => IsValidEffectSetup() ? _effectInstances[WrapIndex(_currentEffectIndex + 1)] : null;
        
        /// <summary>
        /// Check if we have the minimum required effects for proper operation
        /// </summary>
        private bool IsValidEffectSetup() => _effectInstances != null && _effectInstances.Count >= REQUIRED_EFFECT_COUNT;
        
        #endregion


        #region Public API
        
        /// <summary>
        /// Handle incoming OSC message with texture path
        /// </summary>
        public void OnReceivePath(OscPort.Capsule capsule)
        {
            try
            {
                var message = capsule.message;
                if (message.data == null || message.data.Length == 0)
                {
                    Debug.LogWarning("Main: Received empty OSC message");
                    return;
                }
                
                var texturePath = (string)message.data[0];
                LoadAndSwitchTexture(texturePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Main: Error processing OSC message - {ex.Message}", this);
            }
        }
        
        /// <summary>
        /// Load texture from file and initiate transition
        /// </summary>
        public void LoadAndSwitchTexture(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning("Main: Invalid file path provided", this);
                return;
            }
            
            TextureIO.LoadTextureFromFile(filePath, (loadedTexture) =>
            {
                if (loadedTexture != null)
                {
                    Debug.Log($"Main: Loaded texture from {filePath}", this);
                    StartTransition(loadedTexture);
                }
                else
                {
                    Debug.LogError($"Main: Failed to load texture from {filePath}", this);
                }
            });
        }
        
        /// <summary>
        /// Begin transition to new texture
        /// </summary>
        public void StartTransition(Texture2D newTexture)
        {
            if (!IsValidEffectSetup())
            {
                Debug.LogWarning("Main: Cannot start transition - invalid effect setup", this);
                return;
            }
            
            var nextEffect = NextEffect;
            if (nextEffect?.controller == null)
            {
                Debug.LogWarning("Main: Next effect or controller is null", this);
                return;
            }

            // Set up the next effect with new texture
            nextEffect.source = newTexture;
            nextEffect.controller.Source = newTexture;

            // Stop any existing transition and start new one
            StopCurrentTransition();
            _transitionCoroutine = StartCoroutine(TransitionCoroutine());
        }
        
        /// <summary>
        /// Test method for manual transitions (bound to T key)
        /// </summary>
        [ContextMenu("Test Transition")]
        public void TestTransition()
        {
            if (IsValidEffectSetup())
            {
                StopCurrentTransition();
                _transitionCoroutine = StartCoroutine(TransitionCoroutine());
            }
        }
        
        #endregion

        #region Transition System
        
        /// <summary>
        /// Stop current transition if running
        /// </summary>
        private void StopCurrentTransition()
        {
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }
        }
        
        /// <summary>
        /// Main transition coroutine: Previous → 0, Current → prevFloor, Next → 1
        /// </summary>
        private IEnumerator TransitionCoroutine()
        {
            // 1) Initialize next effect
            var nextEffect = NextEffect;
            if (nextEffect != null)
            {
                // Ensure next effect starts clean
                if (nextEffect.controller != null)
                {
                    nextEffect.controller.Source = nextEffect.source;
                    nextEffect.controller.Ratio = 0f;
                }
                
                nextEffect.ratio = 0f;  // Internal effect starts at 0
                nextEffect.blend = 1f;  // External blend at full (will be controlled by ratio)
            }

            // 2) Allow one frame for controller to generate valid output
            yield return null;

            // 3) Record starting values for smooth interpolation
            var startValues = new TransitionState
            {
                PreviousBlend = PreviousEffect?.blend ?? 0f,
                CurrentBlend = CurrentEffect?.blend ?? 0f,
                NextRatio = NextEffect?.ratio ?? 0f
            };

            // 4) Define target values
            var targetValues = new TransitionState
            {
                PreviousBlend = 0f,
                CurrentBlend = _previousLayerOpacity,
                NextRatio = 1f
            };

            // 5) Animate transition
            yield return StartCoroutine(AnimateTransition(startValues, targetValues));

            // 6) Finalize values and advance to next effect
            FinalizeTransition(targetValues);
            _pixelExtractor.Execute(nextEffect);

            yield return SaveCaptureTexture();
        }
        
        /// <summary>
        /// Animate the transition between start and target values
        /// </summary>
        private IEnumerator AnimateTransition(TransitionState startValues, TransitionState targetValues)
        {
            float elapsedTime = 0f;
            
            while (elapsedTime < _transitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsedTime / Mathf.Max(0.0001f, _transitionDuration));
                
                // Apply easing curve if available
                float easedTime = _transitionCurve != null ? _transitionCurve.Evaluate(normalizedTime) : normalizedTime;

                // Interpolate all values
                if (PreviousEffect != null)
                    PreviousEffect.blend = Mathf.Lerp(startValues.PreviousBlend, targetValues.PreviousBlend, easedTime);

                if (CurrentEffect != null)
                    CurrentEffect.blend = Mathf.Lerp(startValues.CurrentBlend, targetValues.CurrentBlend, easedTime);

                if (NextEffect != null)
                    NextEffect.ratio = Mathf.Lerp(startValues.NextRatio, targetValues.NextRatio, easedTime);

                yield return null;
            }
        }
        
        /// <summary>
        /// Finalize transition by setting exact target values and advancing index
        /// </summary>
        private void FinalizeTransition(TransitionState targetValues)
        {
            // Lock in final values
            if (PreviousEffect != null) PreviousEffect.blend = targetValues.PreviousBlend;
            if (CurrentEffect != null) CurrentEffect.blend = targetValues.CurrentBlend;
            if (NextEffect != null) NextEffect.ratio = targetValues.NextRatio;

            // Advance to next effect
            _currentEffectIndex = WrapIndex(_currentEffectIndex + 1);
            _transitionCoroutine = null;
        }
        
        /// <summary>
        /// Helper struct to hold transition state values
        /// </summary>
        private struct TransitionState
        {
            public float PreviousBlend;
            public float CurrentBlend;
            public float NextRatio;
        }
        
        #endregion

        #region Update and Rendering
        
        void Update()
        {
            if (!IsValidEffectSetup() || _outputMaterial == null)
                return;

            // Update all effect instances
            UpdateEffectInstances();
            
            // Update output material with current textures and blend values
            UpdateOutputMaterial();

            // Handle debug input
            HandleDebugInput();
        }
        
        /// <summary>
        /// Update all effect controller instances
        /// </summary>
        private void UpdateEffectInstances()
        {
            PreviousEffect?.UpdateController();
            CurrentEffect?.UpdateController();
            NextEffect?.UpdateController();
        }
        
        /// <summary>
        /// Update the output material with current textures and blend values
        /// </summary>
        private void UpdateOutputMaterial()
        {
            // Set textures
            _outputMaterial.SetTexture("_Prev", GetSafeTexture(PreviousEffect));
            _outputMaterial.SetTexture("_Current", GetSafeTexture(CurrentEffect));
            _outputMaterial.SetTexture("_Next", GetSafeTexture(NextEffect));

            // Set blend values
            _outputMaterial.SetFloat("_PrevBlend", GetSafeBlendValue(PreviousEffect));
            _outputMaterial.SetFloat("_CurrentBlend", GetSafeBlendValue(CurrentEffect));
            _outputMaterial.SetFloat("_NextBlend", GetSafeBlendValue(NextEffect));
        }
        
        /// <summary>
        /// Handle debug keyboard input
        /// </summary>
        private void HandleDebugInput()
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                TestTransition();
            }
        }
        
        /// <summary>
        /// Get texture from effect instance, with fallback to black texture
        /// </summary>
        private Texture GetSafeTexture(EffectInstance instance)
        {
            if (instance?.controller?.Output != null)
                return instance.controller.Output;
            return Texture2D.blackTexture;
        }

        /// <summary>
        /// Get blend value from effect instance, with fallback to 0
        /// </summary>
        private float GetSafeBlendValue(EffectInstance instance, float fallback = 0f)
        {
            return instance?.blend ?? fallback;
        }
        
        #endregion

        #region Utility Methods
        
        /// <summary>
        /// Save current capture texture to file
        /// </summary>
        private IEnumerator SaveCaptureTexture()
        {
            yield return new WaitForSeconds(1f);
            
            if (_captureTexture == null)
            {
                Debug.LogWarning("Main: No capture texture assigned for saving", this);
                yield break;
            }

            try
            {
                var timestamp = DateTime.Now.ToString("yyMMdd_HHmmss");
                var filename = $"capture_{timestamp}.png";
                var fullPath = Path.Combine(_outputFolder, filename);

                // Ensure directory exists
                Directory.CreateDirectory(_outputFolder);
                
                TextureIO.SaveRenderTextureToPNG(_captureTexture, fullPath);
                Debug.Log($"Main: Saved capture to {fullPath}", this);
                SendPathToOSC(fullPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Main: Failed to save capture texture - {ex.Message}", this);
            }
        }


        private void SendPathToOSC(string path)
        {
            var encoder = new MessageEncoder("/path");
            encoder.Add(path);
            _osc.Send(encoder);
        }
        
        /// <summary>
        /// Validate the current setup and log any issues
        /// </summary>
        [ContextMenu("Validate Setup")]
        public void ValidateSetup()
        {
            var issues = new List<string>();
            
            if (_outputMaterial == null)
                issues.Add("Output material is not assigned");
                
            if (_effectInstances == null || _effectInstances.Count < REQUIRED_EFFECT_COUNT)
                issues.Add($"Need at least {REQUIRED_EFFECT_COUNT} effect instances");
                
            for (int i = 0; i < _effectInstances.Count; i++)
            {
                var effect = _effectInstances[i];
                if (effect?.controller == null)
                    issues.Add($"Effect instance {i} has no controller assigned");
            }
            
            if (issues.Count > 0)
            {
                Debug.LogWarning($"Main setup issues:\n- {string.Join("\n- ", issues)}", this);
            }
            else
            {
                Debug.Log("Main setup is valid!", this);
            }
        }
        
        void OnDestroy()
        {
            StopCurrentTransition();
        }
        
        #endregion
    }

    /// <summary>
    /// Represents a single effect instance with its controller and parameters
    /// </summary>
    [Serializable]
    public class EffectInstance
    {
        [Header("Effect Controller")]
        public EffectController controller;
        
        [Header("Source")]
        public Texture2D source;
        
        [Header("Parameters")]
        [Range(0f, 1f)] public float ratio = 1f;      // Internal effect intensity
        [Range(0f, 1f)] public float blend = 1f;      // External blend amount
        public Color bgColor = Color.black;            // Background color
        
        /// <summary>
        /// Update the associated effect controller with current parameters
        /// </summary>
        public void UpdateController()
        {
            if (controller == null) 
            {
                Debug.LogWarning("EffectInstance: No controller assigned");
                return;
            }
            
            try
            {
                controller.Source = source;
                controller.Ratio = ratio;
                controller.BgColor = bgColor;
            }
            catch (Exception ex)
            {
                Debug.LogError($"EffectInstance: Error updating controller - {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if this effect instance is properly configured
        /// </summary>
        public bool IsValid => controller != null;
        
        /// <summary>
        /// Get the output texture from the controller, if available
        /// </summary>
        public RenderTexture OutputTexture => controller?.Output;
    }
}
