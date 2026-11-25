using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HD2DTest
{
    /// <summary>Follows the target and keeps the depth-of-field focus on it (tilt-shift look).</summary>
    public class HD2DCameraRig : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 11f, -17f);
        public float lookHeight = 1.2f;
        public float smoothing = 5f;
        public Volume volume;
        DepthOfField _dof;

        void Start()
        {
            if (volume != null && volume.profile != null) volume.profile.TryGet(out _dof);
        }

        void LateUpdate()
        {
            if (!target) return;
            Vector3 desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            Vector3 focus = target.position + Vector3.up * lookHeight;
            transform.rotation = Quaternion.LookRotation(focus - (target.position + offset));
            if (_dof != null) _dof.focusDistance.value = Vector3.Distance(transform.position, focus);
        }
    }
}
