using UnityEngine;

public interface IEffect
{
    Texture2D Source { get; set; }
    Color BgColor { get; set; }
    float Ratio { get; set; }
}
