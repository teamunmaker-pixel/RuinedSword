using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HD2DTest
{
    /// <summary>Scrolls the base map (water).</summary>
    public class HD2DUVScroll : MonoBehaviour
    {
        public Vector2 speed = new Vector2(0.03f, 0.015f);
        Material _mat;
        void Start() => _mat = GetComponent<Renderer>().material;
        void Update()
        {
            if (_mat) _mat.SetTextureOffset("_BaseMap", speed * Time.time);
        }
    }
}
