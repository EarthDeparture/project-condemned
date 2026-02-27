// ============================================================================
// IsometricCameraController.cs
// Namespace: Condemned.Gameplay
// Description: Fixed-angle isometric camera that follows a target with smooth
//              damping, scroll-wheel zoom, and event-driven screen shake.
//              Attach directly to the Main Camera GameObject.
//
// Scene Setup:
//   1. Select Main Camera in the Game scene hierarchy
//   2. Add Component → IsometricCameraController
//   3. Assign _target once the player prefab is in the scene (Issue #11)
//   4. Tune _eulerAngles and _distance in the Inspector for your preferred angle
//
// TODO (M6): Integrate CinemachineConfiner3D to clamp camera within map bounds.
// ============================================================================

using System.Collections;
using UnityEngine;
using Condemned.Core;

namespace Condemned.Gameplay
{
    [AddComponentMenu("Condemned/Gameplay/Isometric Camera Controller")]
    public class IsometricCameraController : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Follow Target")]
        [Tooltip("The transform the camera will track. Assign the active player.")]
        [SerializeField] private Transform _target;

        [Tooltip("How quickly the camera catches up to the target. Higher = snappier.")]
        [SerializeField, Range(1f, 25f)] private float _followSmoothing = 8f;

        [Header("Isometric Angle")]
        [Tooltip("Camera rotation in Euler angles. Default gives a classic isometric look.")]
        [SerializeField] private Vector3 _eulerAngles = new Vector3(50f, 45f, 0f);

        [Tooltip("Distance from the target along the camera's back axis.")]
        [SerializeField, Range(5f, 40f)] private float _distance = 15f;

        [Header("Zoom")]
        [SerializeField] private bool _enableZoom = true;

        [Tooltip("How fast the scroll wheel changes zoom distance.")]
        [SerializeField, Range(0.5f, 8f)] private float _zoomSpeed = 3f;

        [Tooltip("Minimum zoom distance (closest to target).")]
        [SerializeField] private float _minDistance = 8f;

        [Tooltip("Maximum zoom distance (furthest from target).")]
        [SerializeField] private float _maxDistance = 28f;

        [Header("Screen Shake")]
        [Tooltip("Default shake magnitude in world units.")]
        [SerializeField, Range(0.01f, 1f)] private float _shakeMagnitude = 0.18f;

        [Tooltip("Default shake duration in seconds.")]
        [SerializeField, Range(0.05f, 1f)] private float _shakeDuration = 0.22f;

        // ─── Private State ────────────────────────────────────────────────────

        private Vector3 _smoothVelocity;
        private Vector3 _shakeOffset;
        private bool    _isShaking;

        // ─── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            // Snap to the correct position immediately on start (no slide-in)
            if (_target != null)
                transform.position = CalculateDesiredPosition();

            transform.rotation = Quaternion.Euler(_eulerAngles);

            // Subscribe to game events that trigger camera shake
            EventBus.Subscribe<SurvivorHitEvent>(OnSurvivorHit);
            EventBus.Subscribe<KillerStunnedEvent>(OnKillerStunned);
            EventBus.Subscribe<SurvivorSacrificedEvent>(OnSurvivorSacrificed);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<SurvivorHitEvent>(OnSurvivorHit);
            EventBus.Unsubscribe<KillerStunnedEvent>(OnKillerStunned);
            EventBus.Unsubscribe<SurvivorSacrificedEvent>(OnSurvivorSacrificed);
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            HandleZoom();
            FollowTarget();
        }

        // ─── Core Behaviour ───────────────────────────────────────────────────

        /// <summary>Smoothly move the camera toward the target's isometric position.</summary>
        private void FollowTarget()
        {
            Vector3 desired = CalculateDesiredPosition() + _shakeOffset;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desired,
                ref _smoothVelocity,
                1f / _followSmoothing
            );

            // Lock rotation every frame — prevents any accidental drift
            transform.rotation = Quaternion.Euler(_eulerAngles);
        }

        /// <summary>Adjust zoom distance via scroll wheel input.</summary>
        private void HandleZoom()
        {
            if (!_enableZoom) return;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _distance = Mathf.Clamp(
                    _distance - scroll * _zoomSpeed,
                    _minDistance,
                    _maxDistance
                );
            }
        }

        /// <summary>Calculates the world-space position the camera should be at.</summary>
        private Vector3 CalculateDesiredPosition()
        {
            // Back-project from the target along the camera's view direction
            Quaternion rotation = Quaternion.Euler(_eulerAngles);
            return _target.position - rotation * Vector3.forward * _distance;
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>Set or change the follow target at runtime (e.g. on player spawn).</summary>
        public void SetTarget(Transform target)
        {
            _target = target;

            // Snap camera to the new target immediately to avoid a visible slide
            if (_target != null)
            {
                transform.position = CalculateDesiredPosition();
                _smoothVelocity   = Vector3.zero;
            }
        }

        /// <summary>Trigger a screen shake with optional magnitude and duration overrides.</summary>
        public void TriggerShake(float magnitude = -1f, float duration = -1f)
        {
            if (_isShaking) return;
            StartCoroutine(C_Shake(
                magnitude < 0f ? _shakeMagnitude : magnitude,
                duration  < 0f ? _shakeDuration  : duration
            ));
        }

        // ─── Coroutines ───────────────────────────────────────────────────────

        /// <summary>
        /// Screen shake coroutine. Applies a random offset that diminishes
        /// over the duration, then resets cleanly.
        /// </summary>
        private IEnumerator C_Shake(float magnitude, float duration)
        {
            _isShaking = true;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float strength = Mathf.Lerp(magnitude, 0f, elapsed / duration);
                _shakeOffset   = Random.insideUnitSphere * strength;
                elapsed       += Time.deltaTime;
                yield return null;
            }

            _shakeOffset = Vector3.zero;
            _isShaking   = false;
        }

        // ─── Event Handlers ───────────────────────────────────────────────────

        private void OnSurvivorHit(SurvivorHitEvent e)      => TriggerShake(_shakeMagnitude * 1.4f, _shakeDuration);
        private void OnKillerStunned(KillerStunnedEvent e)  => TriggerShake(_shakeMagnitude * 0.8f, _shakeDuration * 0.6f);
        private void OnSurvivorSacrificed(SurvivorSacrificedEvent e) => TriggerShake(_shakeMagnitude * 2f, _shakeDuration * 1.5f);

        // ─── Editor ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.8f);

            if (_target != null)
            {
                Vector3 desiredPos = CalculateDesiredPosition();
                Gizmos.DrawLine(desiredPos, _target.position);
                Gizmos.DrawWireSphere(_target.position, 0.4f);
                Gizmos.DrawWireSphere(desiredPos, 0.2f);
            }

            // Draw zoom range arc in scene view
            Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
            Quaternion rot = Quaternion.Euler(_eulerAngles);
            Vector3 origin = transform.position;
            Gizmos.DrawRay(origin, rot * Vector3.forward * _minDistance);
            Gizmos.DrawRay(origin, rot * Vector3.forward * _maxDistance);
        }

        private void OnValidate()
        {
            // Keep distance clamped within zoom range when edited in inspector
            _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
        }
#endif
    }
}
