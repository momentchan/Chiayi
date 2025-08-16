using UnityEngine;
using UnityEngine.VFX;

public class ExtractPoints : MonoBehaviour
{
    public ComputeShader compute;
    public Texture2D source;
    public VisualEffect vfx;
    [Range(0, 0.01f)] public float epsilon = 1f / 255f;

    GraphicsBuffer outBuf{
        get{
            if(_outBuf == null){
                _outBuf = new GraphicsBuffer(GraphicsBuffer.Target.Append, maxPoints, sizeof(float) * 2);
                _outBuf.SetCounterValue(0);
            }
            return _outBuf;
        }
    }
    GraphicsBuffer countBuf{
        get{
            if(_countBuf == null){
                _countBuf = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 1, sizeof(uint));
            }
            return _countBuf;
        }
    }
    int kernel;

    GraphicsBuffer _outBuf;
    GraphicsBuffer _countBuf;

    private int w  => source.width;
    private int h=> source.height;
    private int maxPoints=> w * h;
    

    [ContextMenu("Extract")]
    void Extract()
    {
        kernel = compute.FindKernel("ExtractUV");
        compute.SetTexture(kernel, "_Tex", source);
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
    }

    void OnDestroy()
    {
        outBuf?.Dispose();
        countBuf?.Dispose();
    }
}
