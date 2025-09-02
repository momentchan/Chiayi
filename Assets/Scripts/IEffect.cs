using UnityEngine;

public interface IEffect
{
    Texture2D Source { get; set; }
    Texture2D Gradient { get; set; }
    float Ratio { get; set; }
}
