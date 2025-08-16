using Osc;
using UnityEngine;

namespace Chiyi
{
    [ExecuteInEditMode]
    public class Main : MonoBehaviour
    {
        [SerializeField]
        private Texture2D _source;

        [SerializeField, Range(0, 1)]
        private float _ratio;

        public void OnReceivePath(OscPort.Capsule c) { }

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
