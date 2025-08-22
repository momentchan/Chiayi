using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chiayi
{
    /// <summary>
    /// Provides comprehensive validation for the effect system
    /// </summary>
    public static class EffectValidator
    {
        /// <summary>
        /// Validate a single effect instance
        /// </summary>
        public static ValidationResult ValidateEffectInstance(EffectInstance effect, int index = -1)
        {
            var issues = new List<string>();
            var prefix = index >= 0 ? $"Effect {index}" : "Effect";

            if (effect == null)
            {
                issues.Add($"{prefix}: Instance is null");
                return new ValidationResult(issues);
            }

            if (effect.controller == null)
                issues.Add($"{prefix}: Controller is not assigned");

            if (effect.source == null)
                issues.Add($"{prefix}: Source texture is not assigned");

            if (effect.ratio < 0f || effect.ratio > 1f)
                issues.Add($"{prefix}: Ratio ({effect.ratio}) is outside valid range [0,1]");

            if (effect.blend < 0f || effect.blend > 1f)
                issues.Add($"{prefix}: Blend ({effect.blend}) is outside valid range [0,1]");

            return new ValidationResult(issues);
        }

        /// <summary>
        /// Validate a list of effect instances
        /// </summary>
        public static ValidationResult ValidateEffectInstances(List<EffectInstance> effects, int requiredCount = 3)
        {
            var issues = new List<string>();

            if (effects == null)
            {
                issues.Add("Effect instances list is null");
                return new ValidationResult(issues);
            }

            if (effects.Count < requiredCount)
                issues.Add($"Need at least {requiredCount} effect instances, found {effects.Count}");

            for (int i = 0; i < effects.Count; i++)
            {
                var result = ValidateEffectInstance(effects[i], i);
                issues.AddRange(result.Issues);
            }

            return new ValidationResult(issues);
        }

        /// <summary>
        /// Validate effect controller setup
        /// </summary>
        public static ValidationResult ValidateEffectController(EffectController controller)
        {
            var issues = new List<string>();

            if (controller == null)
            {
                issues.Add("Effect controller is null");
                return new ValidationResult(issues);
            }

            // Check if controller has required components
            if (controller.Output == null)
                issues.Add("Effect controller output texture is null");

            if (controller.Source == null)
                issues.Add("Effect controller source texture is null");

            return new ValidationResult(issues);
        }

        /// <summary>
        /// Validate pixel extractor setup
        /// </summary>
        public static ValidationResult ValidatePixelExtractor(PixelExtractor extractor)
        {
            var issues = new List<string>();

            if (extractor == null)
            {
                issues.Add("Pixel extractor is null");
                return new ValidationResult(issues);
            }

            // Note: We can't directly access private fields, but we can check public methods
            // In a real implementation, you might want to add public validation methods to PixelExtractor

            return new ValidationResult(issues);
        }

        /// <summary>
        /// Comprehensive validation of the entire effect system
        /// </summary>
        public static ValidationResult ValidateEffectSystem(
            List<EffectInstance> effects,
            EffectController[] controllers,
            PixelExtractor extractor,
            EffectConfiguration config)
        {
            var issues = new List<string>();

            // Validate configuration
            if (config != null)
            {
                var configResult = config.Validate();
                issues.AddRange(configResult.Issues);
            }
            else
            {
                issues.Add("Effect configuration is not assigned");
            }

            // Validate effect instances
            var requiredCount = config?.requiredEffectCount ?? 3;
            var effectsResult = ValidateEffectInstances(effects, requiredCount);
            issues.AddRange(effectsResult.Issues);

            // Validate controllers
            if (controllers != null)
            {
                for (int i = 0; i < controllers.Length; i++)
                {
                    var controllerResult = ValidateEffectController(controllers[i]);
                    foreach (var issue in controllerResult.Issues)
                    {
                        issues.Add($"Controller {i}: {issue}");
                    }
                }
            }

            // Validate pixel extractor
            var extractorResult = ValidatePixelExtractor(extractor);
            issues.AddRange(extractorResult.Issues);

            return new ValidationResult(issues);
        }

        /// <summary>
        /// Log validation results with appropriate log level
        /// </summary>
        public static void LogValidationResult(ValidationResult result, UnityEngine.Object context = null)
        {
            if (result.IsValid)
            {
                Debug.Log("Effect validation passed!", context);
            }
            else
            {
                Debug.LogWarning($"Effect validation failed:\n{result}", context);
            }
        }
    }
}
