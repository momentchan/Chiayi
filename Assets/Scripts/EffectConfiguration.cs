using UnityEngine;

namespace Chiayi
{
    /// <summary>
    /// Centralized configuration for effect system settings
    /// </summary>
    [CreateAssetMenu(fileName = "EffectConfig", menuName = "Chiayi/Effect Configuration")]
    public class EffectConfiguration : ScriptableObject
    {
        [Header("Transition Settings")]
        [SerializeField, Range(0f, 20f)] public float transitionDuration = 10f;
        [SerializeField, Range(0f, 1f)] public float previousLayerOpacity = 0.2f;
        [SerializeField] public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] public AnimationCurve pixelExtractorCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Performance Settings")]
        [SerializeField] public Vector2Int defaultTextureSize = new(1920, 1080);
        [SerializeField] public RenderTextureFormat defaultFormat = RenderTextureFormat.ARGBFloat;
        [SerializeField, Range(1, 10)] public int requiredEffectCount = 3;

        [Header("Pixel Extraction")]
        [SerializeField, Range(0f, 1f)] public float brightnessThreshold = 0.1f;
        [SerializeField, Range(1, 100)] public int spawnRate = 32;
        [SerializeField] public bool usePerceptualBrightness = true;

        [Header("Output Settings")]
        [SerializeField] public string outputFolder = "C:/Chiayi/";
        [SerializeField] public string captureFilenameFormat = "capture_{0}.png";

        [Header("OSC Settings")]
        [SerializeField] public string uploadPathOscAddress = "/UploadPath";

        [Header("Validation")]
        [SerializeField] public bool enableValidation = true;
        [SerializeField] public bool logDebugInfo = true;

        #region Validation

        /// <summary>
        /// Validate configuration settings
        /// </summary>
        public ValidationResult Validate()
        {
            var issues = new System.Collections.Generic.List<string>();

            if (transitionDuration <= 0f)
                issues.Add("Transition duration must be greater than 0");

            if (previousLayerOpacity < 0f || previousLayerOpacity > 1f)
                issues.Add("Previous layer opacity must be between 0 and 1");

            if (defaultTextureSize.x <= 0 || defaultTextureSize.y <= 0)
                issues.Add("Default texture size must be positive");

            if (requiredEffectCount < 1)
                issues.Add("Required effect count must be at least 1");

            if (brightnessThreshold < 0f || brightnessThreshold > 1f)
                issues.Add("Brightness threshold must be between 0 and 1");

            if (spawnRate < 1)
                issues.Add("Spawn rate must be at least 1");

            if (string.IsNullOrEmpty(outputFolder))
                issues.Add("Output folder cannot be empty");

            if (string.IsNullOrEmpty(uploadPathOscAddress))
                issues.Add("OSC address cannot be empty");

            return new ValidationResult(issues);
        }

        /// <summary>
        /// Get formatted capture filename with timestamp
        /// </summary>
        public string GetCaptureFilename()
        {
            var timestamp = System.DateTime.Now.ToString("yyMMdd_HHmmss");
            return string.Format(captureFilenameFormat, timestamp);
        }

        /// <summary>
        /// Get full capture file path
        /// </summary>
        public string GetCaptureFilePath()
        {
            return System.IO.Path.Combine(outputFolder, GetCaptureFilename());
        }

        #endregion
    }

    /// <summary>
    /// Result of configuration validation
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid => Issues.Count == 0;
        public System.Collections.Generic.List<string> Issues { get; }

        public ValidationResult(System.Collections.Generic.List<string> issues)
        {
            Issues = issues ?? new System.Collections.Generic.List<string>();
        }

        public override string ToString()
        {
            if (IsValid)
                return "Configuration is valid";

            return $"Configuration has {Issues.Count} issues:\n- {string.Join("\n- ", Issues)}";
        }
    }
}
