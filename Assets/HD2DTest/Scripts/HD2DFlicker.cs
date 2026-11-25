using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HD2DTest
{
    /// <summary>Gentle lantern flicker.</summary>
    [RequireComponent(typeof(Light))]
    public class HD2DFlicker : MonoBehaviour
    {
        public float amount = 0.18f;
        public float rate = 3f;
        Light _light;
        float _base, _seed;
        void Start() { _light = GetComponent<Light>(); _base = _light.intensity; _seed = Random.value * 100f; }
        void Update() => _light.intensity = _base * (1f + (Mathf.PerlinNoise(_seed, Time.time * rate) - 0.5f) * 2f * amount);
    }
}
