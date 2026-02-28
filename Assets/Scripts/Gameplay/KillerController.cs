// ============================================================================
// KillerController.cs
// Namespace: Condemned.Gameplay
// Description: Killer character controller. Handles movement, attack/lunge
//              input, footstep events, and interaction. Killer has no sprint —
//              the Bloodlust mechanic (M5) provides catch-up speed boosts.
//              Terror radius broadcasting is implemented in M5.
//
// Scene Setup:
//   1. Add to the Killer prefab root GameObject
//   2. Ensure a CharacterController component is on the root
//   3. Assign _inputActions (CondemendInputActions in Assets/Settings/)
//   4. Assign _cameraTransform (Main Camera transform)
//   5. Assign _attackOrigin (empty child transform at chest/weapon level)
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using Condemned.Core;

namespace Condemned.Gameplay
{
    [AddComponentMenu("Condemned/Gameplay/Killer Controller")]
    [RequireComponent(typeof(CharacterController))]
    public class KillerController : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Input")]
        [SerializeField] private InputActionAsset _inputActions;

        [Header("Movement")]
        [Tooltip("Base movement speed. Slightly faster than a healthy survivor's walk.")]
        [SerializeField] private float _moveSpeed    = 5.5f;

        [Tooltip("Speed while carrying a downed survivor.")]
        [SerializeField] private float _carrySpeed   = 3.8f;

        [Tooltip("Speed multiplier applied during a lunge.")]
        [SerializeField] private float _lungeSpeedMultiplier = 2.2f;

        [Tooltip("Duration of the lunge dash in seconds.")]
        [SerializeField] private float _lungeDuration  = 0.3f;

        [Tooltip("Cooldown after a lunge attempt (hit or miss).")]
        [SerializeField] private float _lungeCooldown  = 3.0f;

        [Header("Movement Feel")]
        [SerializeField, Range(4f, 30f)] private float _acceleration = 10f;
        [SerializeField, Range(4f, 30f)] private float _deceleration = 14f;

        [Header("Gravity")]
        [SerializeField] private float _gravity         = -18f;
        [SerializeField] private float _groundedGravity = -2f;

        [Header("Camera")]
        [SerializeField] private Transform _cameraTransform;

        [Header("Interaction")]
        [Tooltip("Origin transform for interaction raycasts (chest/arm level).")]
        [SerializeField] private Transform _attackOrigin;
        [SerializeField] private float     _interactionRange  = 2.2f;
        [SerializeField] private LayerMask _interactionLayers = ~0;

        [Header("Footsteps")]
        [Tooltip("Distance (m) between footstep events.")]
        [SerializeField] private float _footstepStride    = 2.2f;
        [Tooltip("Footstep sound radius — intentionally wider than survivor.")]
        [SerializeField] private float _footstepRadius    = 20f;
        [Tooltip("Intensity 0–1. Killer footsteps are heavier and more audible.")]
        [SerializeField, Range(0f, 1f)] private float _footstepIntensity = 0.55f;

        // ─── Input Actions ────────────────────────────────────────────────────

        private InputAction _moveAction;
        private InputAction _attackAction;
        private InputAction _lungeAction;
        private InputAction _interactAction;

        // ─── Components ───────────────────────────────────────────────────────

        private CharacterController _cc;

        // ─── State ────────────────────────────────────────────────────────────

        private Vector3 _horizontalVelocity;
        private float   _verticalVelocity;
        private bool    _isGrounded;
        private bool    _isCarrying;
        private bool    _isStunned;
        private bool    _isLunging;
        private float   _lungeTimer;
        private float   _lungeCooldownTimer;
        private float   _distanceTravelled;

        // Bloodlust applied externally from M5 BloodlustSystem
        private float _bloodlustSpeedBonus;

        // ─── Properties ───────────────────────────────────────────────────────

        public bool IsMoving   => _horizontalVelocity.magnitude > 0.1f;
        public bool IsCarrying => _isCarrying;
        public bool IsStunned  => _isStunned;
        public bool IsLunging  => _isLunging;
        public bool CanLunge   => _lungeCooldownTimer <= 0f && !_isLunging && !_isStunned;

        // ─── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();

            // Auto-discover camera if not assigned
            if (_cameraTransform == null && Camera.main != null)
                _cameraTransform = Camera.main.transform;

            BindInputActions();

            EventBus.Subscribe<KillerStunnedEvent>(OnStunned);
        }

        private void OnEnable()
        {
            _moveAction?.Enable();
            _attackAction?.Enable();
            _lungeAction?.Enable();
            _interactAction?.Enable();
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _attackAction?.Disable();
            _lungeAction?.Disable();
            _interactAction?.Disable();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<KillerStunnedEvent>(OnStunned);
        }

        private void Update()
        {
            TickTimers();
            CheckGrounded();
            HandleMovement();
            TrackFootsteps();
        }

        // ─── Input Binding ────────────────────────────────────────────────────

        private void BindInputActions()
        {
            if (_inputActions == null)
            {
                Debug.LogError("[KillerController] InputActionAsset not assigned.");
                return;
            }

            var map = _inputActions.FindActionMap("Killer", throwIfNotFound: true);

            _moveAction     = map.FindAction("Move",     throwIfNotFound: true);
            _attackAction   = map.FindAction("Attack",   throwIfNotFound: true);
            _lungeAction    = map.FindAction("Lunge",    throwIfNotFound: true);
            _interactAction = map.FindAction("Interact", throwIfNotFound: true);

            _attackAction.performed   += _ => TryAttack();
            _lungeAction.performed    += _ => TryLunge();
            _interactAction.performed += _ => TryInteract();
        }

        // ─── Timers ───────────────────────────────────────────────────────────

        private void TickTimers()
        {
            if (_lungeCooldownTimer > 0f)
                _lungeCooldownTimer -= Time.deltaTime;

            if (_isLunging)
            {
                _lungeTimer -= Time.deltaTime;
                if (_lungeTimer <= 0f) EndLunge();
            }
        }

        // ─── Ground Check ─────────────────────────────────────────────────────

        private void CheckGrounded() => _isGrounded = _cc.isGrounded;

        // ─── Movement ─────────────────────────────────────────────────────────

        private void HandleMovement()
        {
            if (_isStunned)
            {
                // Stun: rapidly decelerate to zero
                _horizontalVelocity = Vector3.MoveTowards(
                    _horizontalVelocity, Vector3.zero, _deceleration * 3f * Time.deltaTime);
                ApplyMotion();
                return;
            }

            Vector2 input  = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            Vector3 moveDir = ResolveMovementDirection(input);

            float targetSpeed = CurrentMoveSpeed();
            float smoothRate  = moveDir.magnitude > 0.01f ? _acceleration : _deceleration;

            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity, moveDir * targetSpeed, smoothRate * Time.deltaTime);

            // Rotate toward movement direction
            if (_horizontalVelocity.magnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_horizontalVelocity, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 18f * Time.deltaTime);
            }

            ApplyMotion();
        }

        private float CurrentMoveSpeed()
        {
            if (_isCarrying) return _carrySpeed;
            if (_isLunging)  return _moveSpeed * _lungeSpeedMultiplier;
            return _moveSpeed + _bloodlustSpeedBonus;
        }

        private void ApplyMotion()
        {
            _verticalVelocity = _isGrounded
                ? _groundedGravity
                : _verticalVelocity + _gravity * Time.deltaTime;

            _cc.Move((_horizontalVelocity + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }

        private Vector3 ResolveMovementDirection(Vector2 input)
        {
            if (_cameraTransform == null || input.magnitude < 0.01f)
                return Vector3.zero;

            Vector3 fwd   = _cameraTransform.forward; fwd.y   = 0f; fwd.Normalize();
            Vector3 right = _cameraTransform.right;   right.y = 0f; right.Normalize();

            return (fwd * input.y + right * input.x).normalized;
        }

        // ─── Lunge ────────────────────────────────────────────────────────────

        private void TryLunge()
        {
            if (!CanLunge) return;

            _isLunging  = true;
            _lungeTimer = _lungeDuration;

            EventBus.Publish(new KillerAttackEvent
            {
                KillerId = gameObject.GetInstanceID(),
                IsLunge  = true,
            });
        }

        private void EndLunge()
        {
            _isLunging          = false;
            _lungeCooldownTimer = _lungeCooldown;
        }

        // ─── Attack ───────────────────────────────────────────────────────────

        private void TryAttack()
        {
            if (_isStunned || _isLunging) return;

            EventBus.Publish(new KillerAttackEvent
            {
                KillerId = gameObject.GetInstanceID(),
                IsLunge  = false,
            });

            // Actual hit detection is handled by the combat system in M5.
            // This event notifies the camera shake, audio, and animation systems.
        }

        // ─── Interaction ──────────────────────────────────────────────────────

        private void TryInteract()
        {
            if (_isStunned) return;

            Transform origin = _attackOrigin != null ? _attackOrigin : transform;
            if (Physics.Raycast(origin.position, origin.forward,
                out RaycastHit hit, _interactionRange, _interactionLayers))
            {
                // Killers interact with: generators (kick), pallets (break),
                // hooked survivors (pick up carried), hatch (close).
                var interactable = hit.collider.GetComponentInParent<IKillerInteractable>();
                interactable?.KillerInteract(this);
            }
        }

        // ─── Footsteps ────────────────────────────────────────────────────────

        private void TrackFootsteps()
        {
            if (!_isGrounded || !IsMoving) return;

            _distanceTravelled += _horizontalVelocity.magnitude * Time.deltaTime;
            if (_distanceTravelled < _footstepStride) return;

            _distanceTravelled = 0f;
            EventBus.Publish(new SoundEmittedEvent
            {
                Position  = transform.position,
                Radius    = _footstepRadius,
                Intensity = _footstepIntensity,
                Type      = SoundType.Footstep,
            });
        }

        // ─── Public API ───────────────────────────────────────────────────────

        public void SetCamera(Transform cam)  => _cameraTransform = cam;

        /// <summary>Called by M5 BloodlustSystem to apply/remove speed bonus.</summary>
        public void SetBloodlustBonus(float bonus) => _bloodlustSpeedBonus = bonus;

        /// <summary>Called when the killer picks up a downed survivor.</summary>
        public void SetCarrying(bool carrying) => _isCarrying = carrying;

        /// <summary>Disable movement input (e.g. during attack animations).</summary>
        public void SetInputEnabled(bool enabled)
        {
            if (enabled) { _moveAction?.Enable(); }
            else         { _moveAction?.Disable(); }
        }

        // ─── Event Handlers ───────────────────────────────────────────────────

        private void OnStunned(KillerStunnedEvent e)
        {
            if (e.KillerId != gameObject.GetInstanceID()) return;
            StartCoroutine(C_Stun(e.StunDuration));
        }

        private System.Collections.IEnumerator C_Stun(float duration)
        {
            _isStunned = true;
            yield return new WaitForSeconds(duration);
            _isStunned = false;
        }

        // ─── Editor ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Transform origin = _attackOrigin != null ? _attackOrigin : transform;

            // Interaction / attack range
            Gizmos.color = Color.red;
            Gizmos.DrawRay(origin.position, origin.forward * _interactionRange);
            Gizmos.DrawWireSphere(origin.position + origin.forward * _interactionRange, 0.1f);

            // Footstep radius
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.05f);
            Gizmos.DrawWireSphere(transform.position, _footstepRadius);
        }
#endif
    }

    // ─── Killer Interactable Interface ────────────────────────────────────────

    /// <summary>
    /// Implement on objects the killer can interact with:
    /// generators (kick), pallets (break), hatch (close), downed survivors (carry).
    /// </summary>
    public interface IKillerInteractable
    {
        void KillerInteract(KillerController killer);
    }
}
