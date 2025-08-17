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
        [SerializeField] private Texture2D _source;

        [SerializeField, Range(0, 1)] private float _ratio;

        [SerializeField] private RenderTexture _spoutTex;

        [SerializeField]
        private string _outputFolder = "C:/Chiayi/";

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
            if (_source == null)
                return;

            foreach (var source in GetComponentsInChildren<ISource>())
            {
                source.SourceTexture = _source;
                source.Ratio = _ratio;
            }
        }

        void OnDestroy() { }
    }
}
