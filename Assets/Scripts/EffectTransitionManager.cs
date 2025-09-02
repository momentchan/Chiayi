using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chiayi
{
    /// <summary>
    /// Manages transitions between effect instances with smooth interpolation
    /// </summary>
    public class EffectTransitionManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private EffectConfiguration _config;

        [Header("Dependencies")]
        [SerializeField] private PixelExtractor _pixelExtractor;

        // State management
        private int _currentEffectIndex = 0;
        private Coroutine _transitionCoroutine;
        private List<EffectInstance> _effectInstances;

        // Events
        public event Action<EffectInstance> OnTransitionStarted;
        public event Action<EffectInstance> OnTransitionCompleted;
        public event Action<Exception> OnTransitionError;

        #region Public API

        /// <summary>
        /// Initialize the transition manager with effect instances
        /// </summary>
        public void Initialize(List<EffectInstance> effectInstances)
        {
            _effectInstances = effectInstances ?? throw new ArgumentNullException(nameof(effectInstances));
            Debug.Log($"EffectTransitionManager: Initialized with {_effectInstances.Count} effects", this);
        }

        /// <summary>
        /// Start transition to next effect with new texture
        /// </summary>
        public OperationResult<bool> StartTransition(Texture2D newTexture)
        {
            try
            {
                if (!ValidateTransitionSetup())
                    return OperationResult<bool>.Failure("Invalid transition setup");

                var nextEffect = GetNextEffect();
                if (nextEffect?.controller == null)
                    return OperationResult<bool>.Failure("Next effect or controller is null");

                // Set up the next effect with new texture
                nextEffect.source = newTexture;
                nextEffect.controller.Source = newTexture;



                // Stop any existing transition and start new one
                StopCurrentTransition();
                _transitionCoroutine = StartCoroutine(TransitionCoroutine());

                OnTransitionStarted?.Invoke(nextEffect);
                return OperationResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                OnTransitionError?.Invoke(ex);
                return OperationResult<bool>.Failure($"Transition failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test transition without new texture (for debugging)
        /// </summary>
        public OperationResult<bool> TestTransition()
        {
            if (!ValidateTransitionSetup())
                return OperationResult<bool>.Failure("Invalid transition setup");

            StopCurrentTransition();
            _transitionCoroutine = StartCoroutine(TransitionCoroutine());
            return OperationResult<bool>.Success(true);
        }

        /// <summary>
        /// Stop current transition if running
        /// </summary>
        public void StopCurrentTransition()
        {
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }
        }

        /// <summary>
        /// Get current effect index
        /// </summary>
        public int CurrentEffectIndex => _currentEffectIndex;

        /// <summary>
        /// Get current active effect
        /// </summary>
        public EffectInstance CurrentEffect => GetCurrentEffect();

        /// <summary>
        /// Get previous effect
        /// </summary>
        public EffectInstance PreviousEffect => GetPreviousEffect();

        /// <summary>
        /// Get next effect
        /// </summary>
        public EffectInstance NextEffect => GetNextEffect();

        #endregion

        #region Private Methods

        /// <summary>
        /// Validate that transition setup is valid
        /// </summary>
        private bool ValidateTransitionSetup()
        {
            if (_config == null)
            {
                Debug.LogWarning("EffectTransitionManager: Configuration not assigned", this);
                return false;
            }

            var requiredCount = _config.requiredEffectCount;
            if (_effectInstances == null || _effectInstances.Count < requiredCount)
            {
                Debug.LogWarning($"EffectTransitionManager: Need at least {requiredCount} effect instances for transitions", this);
                return false;
            }

            if (_pixelExtractor == null)
            {
                Debug.LogWarning("EffectTransitionManager: PixelExtractor not assigned", this);
                return false;
            }

            return true;
        }

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
        private EffectInstance GetPreviousEffect()
        {
            return IsValidEffectSetup() ? _effectInstances[WrapIndex(_currentEffectIndex - 1)] : null;
        }

        /// <summary>
        /// Get the current active effect instance
        /// </summary>
        private EffectInstance GetCurrentEffect()
        {
            return IsValidEffectSetup() ? _effectInstances[WrapIndex(_currentEffectIndex)] : null;
        }

        /// <summary>
        /// Get the next effect instance in the sequence
        /// </summary>
        private EffectInstance GetNextEffect()
        {
            return IsValidEffectSetup() ? _effectInstances[WrapIndex(_currentEffectIndex + 1)] : null;
        }

        /// <summary>
        /// Check if we have the minimum required effects for proper operation
        /// </summary>
        private bool IsValidEffectSetup() => _effectInstances != null && _effectInstances.Count >= (_config?.requiredEffectCount ?? 3);

        /// <summary>
        /// Main transition coroutine: Previous → 0, Current → prevFloor, Next → 1
        /// </summary>
        private IEnumerator TransitionCoroutine()
        {
            // 1) Initialize next effect
            var nextEffect = GetNextEffect();
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
                PreviousBlend = GetPreviousEffect()?.blend ?? 0f,
                CurrentBlend = GetCurrentEffect()?.blend ?? 0f,
                NextRatio = GetNextEffect()?.ratio ?? 0f
            };

            // 4) Define target values
            var targetValues = new TransitionState
            {
                PreviousBlend = 0f,
                CurrentBlend = _config?.previousLayerOpacity ?? 0.2f,
                NextRatio = 1f
            };

            _pixelExtractor.EnableSpawn(false);
            
            // 5) Animate transition
            yield return StartCoroutine(AnimateTransition(startValues, targetValues));

            // 6) Finalize values and advance to next effect
            FinalizeTransition(targetValues);
            
            _pixelExtractor.EnableSpawn(true);
            _pixelExtractor.Execute(nextEffect);

            OnTransitionCompleted?.Invoke(nextEffect);
        }

        /// <summary>
        /// Animate the transition between start and target values
        /// </summary>
        private IEnumerator AnimateTransition(TransitionState startValues, TransitionState targetValues)
        {
            float elapsedTime = 0f;
            var transitionDuration = _config?.transitionDuration ?? 0.8f;

            while (elapsedTime < transitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsedTime / Mathf.Max(0.0001f, transitionDuration));

                // Apply easing curve if available
                var transitionCurve = _config?.transitionCurve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);
                float easedTime = transitionCurve.Evaluate(normalizedTime);

                var pixelExtractorCurve = _config?.pixelExtractorCurve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);
                _pixelExtractor.SetTransitionRatio(pixelExtractorCurve.Evaluate(easedTime));

                // Interpolate all values
                var previousEffect = GetPreviousEffect();
                if (previousEffect != null)
                    previousEffect.blend = Mathf.Lerp(startValues.PreviousBlend, targetValues.PreviousBlend, easedTime);

                var currentEffect = GetCurrentEffect();
                if (currentEffect != null)
                    currentEffect.blend = Mathf.Lerp(startValues.CurrentBlend, targetValues.CurrentBlend, easedTime);

                var nextEffect = GetNextEffect();
                if (nextEffect != null)
                    nextEffect.ratio = Mathf.Lerp(startValues.NextRatio, targetValues.NextRatio, easedTime);

                yield return null;
            }
        }

        /// <summary>
        /// Finalize transition by setting exact target values and advancing index
        /// </summary>
        private void FinalizeTransition(TransitionState targetValues)
        {
            // Lock in final values
            var previousEffect = GetPreviousEffect();
            if (previousEffect != null) previousEffect.blend = targetValues.PreviousBlend;
            
            var currentEffect = GetCurrentEffect();
            if (currentEffect != null) currentEffect.blend = targetValues.CurrentBlend;
            
            var nextEffect = GetNextEffect();
            if (nextEffect != null) nextEffect.ratio = targetValues.NextRatio;

            // Advance to next effect
            _currentEffectIndex = WrapIndex(_currentEffectIndex + 1);
            _transitionCoroutine = null;
        }

        #endregion

        #region Helper Classes

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

        #region Unity Lifecycle

        void OnDestroy()
        {
            StopCurrentTransition();
        }

        #endregion
    }

    /// <summary>
    /// Result pattern for operations that can succeed or fail
    /// </summary>
    public class OperationResult<T>
    {
        public bool IsSuccess { get; private set; }
        public T Data { get; private set; }
        public string ErrorMessage { get; private set; }
        
        public static OperationResult<T> Success(T data) => new() { IsSuccess = true, Data = data };
        public static OperationResult<T> Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
    }
}
