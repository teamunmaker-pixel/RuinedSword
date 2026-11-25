using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HD2DTest
{
    /// <summary>Plays a grid sprite sheet (cols x rows) on a quad by changing _BaseMap_ST.
    /// Row order from top: 0 = down, 1 = left, 2 = right, 3 = up.</summary>
    [RequireComponent(typeof(Renderer))]
    public class HD2DSheetAnimator : MonoBehaviour
    {
        public int columns = 3;
        public int rows = 4;
        public float fps = 7f;
        public int direction;
        public bool moving;

        static readonly int[] Cycle = { 0, 1, 2, 1 };
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        Renderer _renderer;
        MaterialPropertyBlock _block;
        float _time;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
            Apply(1);
        }

        void Update()
        {
            int col = 1;
            if (moving)
            {
                _time += Time.deltaTime * fps;
                col = Cycle[(int)_time % Cycle.Length];
            }
            else _time = 0f;
            Apply(col);
        }

        void Apply(int col)
        {
            float sx = 1f / columns, sy = 1f / rows;
            float oy = 1f - (direction + 1) * sy;
            _renderer.GetPropertyBlock(_block);
            _block.SetVector(BaseMapST, new Vector4(sx, sy, col * sx, oy));
            _renderer.SetPropertyBlock(_block);
        }
    }
}
