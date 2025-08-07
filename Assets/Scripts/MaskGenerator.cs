using System.Collections.Generic;
using UnityEngine;

namespace Chiyi
{
    [ExecuteInEditMode]
    public class MaskGenerator : MonoBehaviour
    {
        [SerializeField] private Vector3 _targetHSV;
        [SerializeField] private Vector3 _filterRange;
        [SerializeField] private Vector2 _smoothRange;
        [SerializeField] private List<Material> _materials;

        void Update()
        {
            foreach (var m in _materials)
            {
                if (m == null) continue;

                m.SetVector("_TargetHSV", _targetHSV);
                m.SetVector("_FilterRange", _filterRange);
                m.SetVector("_SmoothRange", _smoothRange);
            }
        }
    }
}