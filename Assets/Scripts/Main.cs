using System;
using System.IO;
using System.Collections;
using Osc;
using UnityEngine;

namespace Chiyi
{
    [ExecuteInEditMode]
    public class Main : MonoBehaviour
    {
        [SerializeField] private Material _outputMat;
        [SerializeField] private Texture2D _source;

        [SerializeField, Range(0, 1)] private float _globalRatio;

        [SerializeField] private RenderTexture _spoutTex;

        [SerializeField] private string _outputFolder = "C:/Chiayi/";


        [SerializeField] private EffectInstance _previous;
        [SerializeField] private EffectInstance _current;



        public void OnReceivePath(OscPort.Capsule c)
        {

            try
            {
                var msg = c.message;
                var path = (string)msg.data[0];

                TextureIO.LoadTextureFromFile(path, (tex) =>
                {
                    Debug.Log("Loaded texture: " + path);

                    _source = tex;

                    StartCoroutine(SaveTexture());
                });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        IEnumerator SaveTexture()
        {
            yield return new WaitForSeconds(1);
            var timestamp = System.DateTime.Now.ToString("yyMMdd_HHmmss");
            var filename = $"output_{timestamp}.png";
            var fullPath = Path.Combine(_outputFolder, filename);

            TextureIO.SaveRenderTextureToPNG(_spoutTex, fullPath);
        }

        void Update()
        {
            if (_current != null)
            {
                _current.controller.Source = _current.source;
                _current.controller.Ratio = _current.ratio;

                _outputMat.SetTexture("_Current", _current.controller.Output);
            }

            if (_previous != null)
            {
                _previous.controller.Source = _previous.source;
                _previous.controller.Ratio = _previous.ratio;

                _outputMat.SetTexture("_Prev", _previous.controller.Output);
            }

            _outputMat.SetFloat("_Ratio", _globalRatio);
        }

        void OnDestroy() { }
    }

    [System.Serializable]
    public class EffectInstance
    {
        public EffectController controller;
        public Texture2D source;
        [Range(0, 1)] public float ratio;
    }
}
