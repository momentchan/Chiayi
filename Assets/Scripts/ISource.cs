using UnityEngine;

public interface ISource
{
    Texture2D SourceTexture { get; set; }
    float Ratio { get; set; }
}
