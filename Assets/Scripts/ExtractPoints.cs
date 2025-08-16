using UnityEngine;
using UnityEngine.VFX;

namespace Chiyi
{
    public class ExtractPoints : MonoBehaviour, ISource
    {
        [SerializeField]
        private VisualEffect vfx;

        [SerializeField]
        private ComputeShader compute;

        [SerializeField]
        [Range(0, 0.01f)]
        private float epsilon = 1f / 255f;

        public float Ratio { get; set; }

        public Texture2D SourceTexture { get; set; }

        private GraphicsBuffer outBuf
        {
            get
            {
                if (_outBuf == null)
                {
                    _outBuf = new GraphicsBuffer(
                        GraphicsBuffer.Target.Append,
                        maxPoints,
                        sizeof(float) * 2
                    );
                    _outBuf.SetCounterValue(0);
                }
                return _outBuf;
            }
        }
        private GraphicsBuffer countBuf
        {
            get
            {
                if (_countBuf == null)
                {
                    _countBuf = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 1, sizeof(uint));
                }
                return _countBuf;
            }
        }
        private int kernel;

        private GraphicsBuffer _outBuf;
        private GraphicsBuffer _countBuf;

        private int w => SourceTexture.width;
        private int h => SourceTexture.height;
        private int maxPoints => w * h;

        [ContextMenu("Extract")]
        void Extract()
        {
            kernel = compute.FindKernel("ExtractUV");
            compute.SetTexture(kernel, "_Tex", SourceTexture);
            compute.SetInts("_Size", w, h);
            compute.SetFloat("_Epsilon", epsilon);
            compute.SetBuffer(kernel, "_OutUV", outBuf);

            compute.Dispatch(kernel, (w + 7) / 8, (h + 7) / 8, 1);

            GraphicsBuffer.CopyCount(outBuf, countBuf, 0);
            uint[] countArr = new uint[1];
            countBuf.GetData(countArr);
            uint pointCount = countArr[0];
            Debug.Log($"points={pointCount}");

            vfx.SetGraphicsBuffer("SamplesUV", outBuf);
            vfx.SetUInt("SamplesCount", pointCount);
            vfx.SetTexture("SourceTex", SourceTexture);
        }

        void OnDestroy()
        {
            outBuf?.Dispose();
            countBuf?.Dispose();
        }
    }
}
