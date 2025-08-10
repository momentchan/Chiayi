using mj.gist;
using UnityEngine;

namespace Chiyi
{
    public class TrailEffect : MonoBehaviour
    {
        [SerializeField] private PingPongRenderTexture _rt;
        [SerializeField] private Material _mat;
        [SerializeField] private RenderTexture _source;
        [SerializeField] private RenderTexture _output;


        [SerializeField] private int steps;
        [SerializeField] private float span = 0.1f;
        
        private PingPongRenderTexture rt
        {
            get
            {
                if (_rt == null)
                    _rt = new PingPongRenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.ARGB32);
                return _rt;
            }
        }

        private Vector2Int resolution = new Vector2Int(1920, 1200);

        private void Render()
        {
            Graphics.Blit(Texture2D.blackTexture, rt.Read);

            for (var i = 0; i < steps; i++)
            {
                _mat.SetTexture("_Current", _source);
                _mat.SetTexture("_Prev", rt.Read);
                _mat.SetFloat("_Offset", span * (float)i / steps);
                Graphics.Blit(Texture2D.blackTexture, rt.Write, _mat);
                rt.Swap();
            }

            Graphics.Blit(rt.Read, _output);
        }

        void Update()
        {
            if (Input.GetKey(KeyCode.Space))
            {
                Render();
            }
        }

        private void OnDisable()
        {
            Release();
        }

        private void OnDestroy()
        {
            Release();
        }

        private void Release()
        {
            if (_rt != null)
            {
                _rt.Dispose();
                _rt = null;
            }
        }
    }
}