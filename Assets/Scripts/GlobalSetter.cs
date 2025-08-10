using UnityEngine;


namespace Chiyi
{
    [ExecuteInEditMode]
    public class GlobalSetter : MonoBehaviour
    {
        [SerializeField] private Texture2D _sourceTex;

        void Update()
        {
            Shader.SetGlobalTexture("_SourceTex", _sourceTex);
        }
    }
}