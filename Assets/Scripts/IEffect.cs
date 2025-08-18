using UnityEngine;

public interface IEffect
{
    Texture2D Source { get; set; }
    RenderTexture Output { get; }
    float Ratio { get; set; }

}
