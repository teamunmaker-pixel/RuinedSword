using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HD2DTest
{
    /// <summary>Simple 8-way mover that follows the voxel ground height.</summary>
    public class HD2DPlayerController : MonoBehaviour
    {
        public float speed = 4.5f;
        public float stepHeight = 1.1f;
        public float minWalkableHeight = 0.5f;
        HD2DSheetAnimator _anim;

        void Start() => _anim = GetComponentInChildren<HD2DSheetAnimator>();

        void Update()
        {
            Vector2 input = ReadInput();
            bool moving = input.sqrMagnitude > 0.01f;
            if (_anim) _anim.moving = moving;
            if (!moving) { SnapToGround(); return; }

            input = Vector2.ClampMagnitude(input, 1f);
            if (_anim)
            {
                if (Mathf.Abs(input.x) > Mathf.Abs(input.y)) _anim.direction = input.x > 0 ? 2 : 1;
                else _anim.direction = input.y > 0 ? 3 : 0;
            }

            Vector3 step = new Vector3(input.x, 0f, input.y) * speed * Time.deltaTime;
            TryMove(new Vector3(step.x, 0, 0));
            TryMove(new Vector3(0, 0, step.z));
            SnapToGround();
        }

        void TryMove(Vector3 delta)
        {
            if (delta.sqrMagnitude < 1e-8f) return;
            Vector3 probe = transform.position + delta + delta.normalized * 0.35f;
            if (!SampleGround(probe, out float h)) return;
            if (h < minWalkableHeight) return;                       // water
            if (h - transform.position.y > stepHeight) return;        // cliff
            transform.position += delta;
        }

        void SnapToGround()
        {
            if (SampleGround(transform.position, out float h))
            {
                var p = transform.position;
                p.y = Mathf.MoveTowards(p.y, h, 10f * Time.deltaTime);
                transform.position = p;
            }
        }

        static bool SampleGround(Vector3 at, out float height)
        {
            height = 0f;
            if (Physics.Raycast(at + Vector3.up * 20f, Vector3.down, out var hit, 50f))
            {
                height = hit.point.y;
                return true;
            }
            return false;
        }

        static Vector2 ReadInput()
        {
            Vector2 v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
            }
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp != null) v += gp.leftStick.ReadValue();
#else
            v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
            return v;
        }
    }
}
