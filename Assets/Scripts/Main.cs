using System;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using UnityEngine;

namespace Chiayi
{
    [ExecuteInEditMode]
    public class Main : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private EffectConfiguration _config;
        
        [Header("Output Configuration")]
        [SerializeField] private Material _outputMaterial;
        [SerializeField] private Material _captureMaterial;
        [SerializeField] private RenderTexture _captureTexture;

        [Header("Effect Instances")]
        [SerializeField] private List<EffectInstance> _effectInstances = new List<EffectInstance>();

        [Header("Dependencies")]
        [SerializeField] private EffectTransitionManager _transitionManager;
        [SerializeField] private OscMessageHandler _oscHandler;

        // Events
        public event Action<Texture2D> OnTextureLoaded;
        public event Action<string> OnCaptureSaved;
        public event Action<Exception> OnError;

        #region Initialization

        void Start()
        {
            InitializeComponents();
            SubscribeToEvents();
        }

        /// <summary>
        /// Initialize all components and validate setup
        /// </summary>
        private void InitializeComponents()
        {
            // Initialize transition manager
            if (_transitionManager != null)
            {
                _transitionManager.Initialize(_effectInstances);
            }

            // Validate configuration
            if (_config != null)
            {
                var validation = _config.Validate();
                if (!validation.IsValid)
                {
                    Debug.LogWarning($"Main: Configuration validation failed:\n{validation}", this);
                }
            }

            Debug.Log("Main: Components initialized", this);
        }

        /// <summary>
        /// Subscribe to component events
        /// </summary>
        private void SubscribeToEvents()
        {
            if (_oscHandler != null)
            {
                _oscHandler.OnTexturePathReceived += LoadAndSwitchTexture;
                _oscHandler.OnOscError += OnError;
            }

            if (_transitionManager != null)
            {
                _transitionManager.OnTransitionStarted += OnTransitionStarted;
                _transitionManager.OnTransitionCompleted += OnTransitionCompleted;
                _transitionManager.OnTransitionError += OnError;
            }
        }

        #endregion

        #region Effect Instance Management

        /// <summary>
        /// Get the previous effect instance in the sequence
        /// </summary>
        private EffectInstance PreviousEffect => _transitionManager?.PreviousEffect;

        /// <summary>
        /// Get the current active effect instance
        /// </summary>
        private EffectInstance CurrentEffect => _transitionManager?.CurrentEffect;

        /// <summary>
        /// Get the next effect instance in the sequence
        /// </summary>
        private EffectInstance NextEffect => _transitionManager?.NextEffect;

        /// <summary>
        /// Check if we have the minimum required effects for proper operation
        /// </summary>
        private bool IsValidEffectSetup() => _effectInstances != null && _effectInstances.Count >= (_config?.requiredEffectCount ?? 3);

        #endregion


        #region Public API

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
                    OnTextureLoaded?.Invoke(loadedTexture);
                    StartTransition(loadedTexture);
                }
                else
                {
                    var error = new Exception($"Failed to load texture from {filePath}");
                    Debug.LogError($"Main: {error.Message}", this);
                    OnError?.Invoke(error);
                }
            });
        }

        /// <summary>
        /// Begin transition to new texture
        /// </summary>
        public void StartTransition(Texture2D newTexture)
        {
            if (_transitionManager == null)
            {
                Debug.LogWarning("Main: Transition manager not assigned", this);
                return;
            }

            var result = _transitionManager.StartTransition(newTexture);
            if (!result.IsSuccess)
            {
                Debug.LogWarning($"Main: {result.ErrorMessage}", this);
            }
        }

        /// <summary>
        /// Test method for manual transitions (bound to T key)
        /// </summary>
        [ContextMenu("Test Transition")]
        public void TestTransition()
        {
            if (_transitionManager == null)
            {
                Debug.LogWarning("Main: Transition manager not assigned", this);
                return;
            }

            var result = _transitionManager.TestTransition();
            if (!result.IsSuccess)
            {
                Debug.LogWarning($"Main: {result.ErrorMessage}", this);
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle transition started event
        /// </summary>
        private void OnTransitionStarted(EffectInstance effect)
        {
            Debug.Log($"Main: Transition started to effect {effect?.controller?.name ?? "unknown"}", this);
        }

        /// <summary>
        /// Handle transition completed event
        /// </summary>
        private void OnTransitionCompleted(EffectInstance effect)
        {
            Debug.Log($"Main: Transition completed to effect {effect?.controller?.name ?? "unknown"}", this);
            StartCoroutine(SaveCaptureTexture());
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
            if (CurrentEffect?.controller?.Output == null)
            {
                Debug.LogWarning("Main: No current effect output to capture", this);
                yield break;
            }

            _captureMaterial.SetTexture("_MainTex", CurrentEffect.controller.Output);

            yield return new WaitForSeconds(1f);

            if (_captureTexture == null)
            {
                Debug.LogWarning("Main: No capture texture assigned for saving", this);
                yield break;
            }

            try
            {
                var outputFolder = _config?.outputFolder ?? "C:/Chiayi/";
                var filename = _config?.GetCaptureFilename() ?? $"capture_{DateTime.Now:yyMMdd_HHmmss}.png";
                var fullPath = Path.Combine(outputFolder, filename);

                // Ensure directory exists
                Directory.CreateDirectory(outputFolder);

                TextureIO.SaveRenderTextureToPNG(_captureTexture, fullPath);
                Debug.Log($"Main: Saved capture to {fullPath}", this);
                
                OnCaptureSaved?.Invoke(fullPath);
                
                // Send path via OSC if handler is available
                if (_oscHandler != null)
                {
                    _oscHandler.SendPath(fullPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Main: Failed to save capture texture - {ex.Message}", this);
                OnError?.Invoke(ex);
            }
        }

        /// <summary>
        /// Validate the current setup and log any issues
        /// </summary>
        [ContextMenu("Validate Setup")]
        public void ValidateSetup()
        {
            var issues = new List<string>();

            if (_config == null)
                issues.Add("Effect configuration is not assigned");

            if (_outputMaterial == null)
                issues.Add("Output material is not assigned");

            var requiredCount = _config?.requiredEffectCount ?? 3;
            if (_effectInstances == null || _effectInstances.Count < requiredCount)
                issues.Add($"Need at least {requiredCount} effect instances");

            for (int i = 0; i < _effectInstances.Count; i++)
            {
                var effect = _effectInstances[i];
                if (effect?.controller == null)
                    issues.Add($"Effect instance {i} has no controller assigned");
            }

            if (_transitionManager == null)
                issues.Add("Transition manager is not assigned");

            if (_oscHandler == null)
                issues.Add("OSC message handler is not assigned");

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
            // Cleanup is handled by individual components
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
