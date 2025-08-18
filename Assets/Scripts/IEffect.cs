using UnityEngine;

public interface IEffect
{
    Texture2D Source { get; set; }
    RenderTexture Output { get; }
    Color BgColor { get; set; }
    float Ratio { get; set; }
}
