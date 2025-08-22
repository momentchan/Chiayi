using System;
using Osc;
using UnityEngine;

namespace Chiayi
{
    /// <summary>
    /// Handles OSC message processing and communication
    /// </summary>
    public class OscMessageHandler : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private EffectConfiguration _config;
        
        [Header("OSC Configuration")]
        [SerializeField] private OscPort _osc;

        // Events
        public event Action<string> OnTexturePathReceived;
        public event Action<Exception> OnOscError;

        #region Unity Lifecycle

        void Start()
        {
            if (_osc == null)
            {
                Debug.LogWarning("OscMessageHandler: No OSC port assigned", this);
            }
        }

        #endregion

        #region Public API


        /// <summary>
        /// Send a file path via OSC
        /// </summary>
        public void SendPath(string path)
        {
            try
            {
                if (_osc == null)
                {
                    Debug.LogWarning("OscMessageHandler: Cannot send path - OSC port not assigned", this);
                    return;
                }

                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogWarning("OscMessageHandler: Cannot send empty path", this);
                    return;
                }

                var oscAddress = _config?.uploadPathOscAddress ?? "/UploadPath";
                var encoder = new MessageEncoder(oscAddress);
                encoder.Add(path);
                _osc.Send(encoder);
                
                Debug.Log($"OscMessageHandler: Sent path via OSC: {path}", this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"OscMessageHandler: Error sending path via OSC - {ex.Message}", this);
                OnOscError?.Invoke(ex);
            }
        }

        /// <summary>
        /// Validate OSC setup
        /// </summary>
        public bool IsValidSetup()
        {
            return _osc != null;
        }

        #endregion

        #region Private Methods

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
                    Debug.LogWarning("OscMessageHandler: Received empty OSC message");
                    return;
                }

                var texturePath = (string)message.data[0];
                if (string.IsNullOrEmpty(texturePath))
                {
                    Debug.LogWarning("OscMessageHandler: Received empty texture path");
                    return;
                }

                Debug.Log($"OscMessageHandler: Received texture path via OSC: {texturePath}", this);
                OnTexturePathReceived?.Invoke(texturePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"OscMessageHandler: Error processing OSC message - {ex.Message}", this);
                OnOscError?.Invoke(ex);
            }
        }

        #endregion
    }
}
