using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HD2DTest
{
    /// <summary>Subtle wind sway for foliage billboards.</summary>
    public class HD2DSway : MonoBehaviour
    {
        public float angle = 1.5f;
        public float rate = 1.2f;
        float _phase;
        Quaternion _base;
        void Start() { _base = transform.localRotation; _phase = transform.position.x * 0.37f + transform.position.z * 0.21f; }
        void Update() => transform.localRotation = _base * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * rate + _phase) * angle);
    }
}
