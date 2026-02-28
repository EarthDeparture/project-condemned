// ============================================================================
// SurvivorController.cs
// Namespace: Condemned.Gameplay
// Description: Survivor character controller. Handles movement, sprint, crouch,
//              interaction raycasting, skill check submission, and footstep
//              sound events.
//
// Dead by Daylight control scheme (PC):
//   WASD          — Move
//   Left Shift    — Sprint (hold)
//   Left Ctrl     — Crouch (toggle)
//   Space         — Primary Interact (hold near generator/gate/etc.)
//                   Also submits Skill Check when one is active
//   Space (hold)  — Held interactions (healing, repairing) handled via
//                   _isHoldingInteract flag polled by IHoldInteractable
//
// Scene Setup:
//   1. Add to the Survivor prefab root GameObject
//   2. Ensure a CharacterController component is also on the root
//   3. Assign _inputActions (CondemendInputActions asset in Assets/Settings/)
//   4. Assign _cameraTransform (Main Camera transform)
//   5. Assign _interactionOrigin (empty child transform at eye level)
// ============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Condemned.Core;
using Condemned.Systems;

namespace Condemned.Gameplay
{
    [AddComponentMenu("Condemned/Gameplay/Survivor Controller")]
    [RequireComponent(typeof(CharacterController))]
    public class SurvivorController : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Input")]
        [Tooltip("Assign the CondemendInputActions asset from Assets/Settings/.")]
        [SerializeField] private InputActionAsset _inputActions;

        [Header("Movement Speeds")]
        [SerializeField] private float _walkSpeed   = 4.0f;
        [SerializeField] private float _sprintSpeed = 6.5f;
        [SerializeField] private float _crouchSpeed = 2.2f;

        [Header("Movement Feel")]
        [SerializeField, Range(4f, 30f)] private float _acceleration  = 14f;
        [SerializeField, Range(4f, 30f)] private float _deceleration  = 20f;
        [SerializeField, Range(0.4f, 1f)] private float _injuredSpeedMultiplier = 0.78f;

        [Header("Crouch")]
        [SerializeField] private float _standHeight         = 1.8f;
        [SerializeField] private float _crouchHeight        = 1.0f;
        [SerializeField] private float _crouchTransitionSpeed = 8f;

        [Header("Gravity")]
        [SerializeField] private float _gravity         = -18f;
        [SerializeField] private float _groundedGravity = -2f;

        [Header("Camera")]
        [SerializeField] private Transform _cameraTransform;

        [Header("Interaction")]
        [SerializeField] private Transform _interactionOrigin;
        [SerializeField] private float     _interactionRange  = 2.5f;
        [SerializeField] private LayerMask _interactionLayers = ~0;

        [Header("Footsteps")]
        [SerializeField] private float _footstepStride    = 2.0f;
        [SerializeField] private float _footstepRadius    = 12f;
        [SerializeField, Range(0f, 1f)] private float _footstepIntensity = 0.25f;

        // ─── Input Actions ────────────────────────────────────────────────────

        private InputAction _moveAction;
        private InputAction _sprintAction;
        private InputAction _crouchAction;
        private InputAction _interactAction;

        // ─── Components ───────────────────────────────────────────────────────

        private CharacterController _cc;

        // ─── State ────────────────────────────────────────────────────────────

        private Vector3 _horizontalVelocity;
        private float   _verticalVelocity;
        private bool    _isSprinting;
        private bool    _isCrouching;
        private bool    _isGrounded;
        private bool    _isInjured;
        private float   _distanceTravelled;
        private float   _targetCCHeight;

        // Held interact state — IHoldInteractable objects poll this each frame
        private bool    _isHoldingInteract;
        private IInteractable _currentHeldInteractable;

        // ─── Properties ───────────────────────────────────────────────────────

        public bool IsMoving         => _horizontalVelocity.magnitude > 0.1f;
        public bool IsSprinting      => _isSprinting && IsMoving;
        public bool IsCrouching      => _isCrouching;
        public bool IsGrounded       => _isGrounded;
        public bool IsHoldingInteract => _isHoldingInteract;

        // ─── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _targetCCHeight = _standHeight;

            // Auto-discover camera if not assigned — eliminates wiring-order dependency
            if (_cameraTransform == null && Camera.main != null)
                _cameraTransform = Camera.main.transform;

            BindInputActions();

            EventBus.Subscribe<SurvivorHitEvent>(OnHit);
            EventBus.Subscribe<SurvivorHealedEvent>(OnHealed);
        }

        private void OnEnable()
        {
            _moveAction?.Enable();
            _sprintAction?.Enable();
            _crouchAction?.Enable();
            _interactAction?.Enable();
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _sprintAction?.Disable();
            _crouchAction?.Disable();
            _interactAction?.Disable();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<SurvivorHitEvent>(OnHit);
            EventBus.Unsubscribe<SurvivorHealedEvent>(OnHealed);
        }

        private void Update()
        {
            CheckGrounded();
            HandleMovement();
            HandleCrouchTransition();
            TrackFootsteps();
        }

        // ─── Input Binding ────────────────────────────────────────────────────

        private void BindInputActions()
        {
            if (_inputActions == null)
            {
                Debug.LogError("[SurvivorController] InputActionAsset not assigned. " +
                               "Run Condemned > Setup Game Scene, or assign it manually.");
                return;
            }

            var map = _inputActions.FindActionMap("Survivor", throwIfNotFound: true);

            _moveAction     = map.FindAction("Move",     throwIfNotFound: true);
            _sprintAction   = map.FindAction("Sprint",   throwIfNotFound: true);

            _crouchAction   = map.FindAction("Crouch",   throwIfNotFound: true);
            _crouchAction.performed += _ => ToggleCrouch();

            _interactAction = map.FindAction("Interact", throwIfNotFound: true);
            // Space pressed: submit skill check OR start interaction
            _interactAction.performed += _ => OnInteractPressed();
            // Space released: end held interaction
            _interactAction.canceled  += _ => OnInteractReleased();
        }

        // ─── Ground Check ─────────────────────────────────────────────────────

        private void CheckGrounded() => _isGrounded = _cc.isGrounded;

        // ─── Movement ─────────────────────────────────────────────────────────

        private void HandleMovement()
        {
            Vector2 input = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            Vector3 moveDir = ResolveMovementDirection(input);

            _isSprinting = (_sprintAction?.IsPressed() ?? false)
                           && !_isCrouching && !_isInjured;

            float targetSpeed = _isCrouching ? _crouchSpeed
                              : _isSprinting ? _sprintSpeed
                              : _walkSpeed;

            if (_isInjured) targetSpeed *= _injuredSpeedMultiplier;

            float smoothRate = moveDir.magnitude > 0.01f ? _acceleration : _deceleration;
            Vector3 targetVelocity = moveDir * targetSpeed;
            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity, targetVelocity, smoothRate * Time.deltaTime);

            if (_horizontalVelocity.magnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_horizontalVelocity, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 16f * Time.deltaTime);
            }

            _verticalVelocity = _isGrounded
                ? _groundedGravity
                : _verticalVelocity + _gravity * Time.deltaTime;

            _cc.Move((_horizontalVelocity + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }

        private Vector3 ResolveMovementDirection(Vector2 input)
        {
            if (_cameraTransform == null || input.magnitude < 0.01f)
                return Vector3.zero;

            Vector3 camForward = _cameraTransform.forward; camForward.y = 0f; camForward.Normalize();
            Vector3 camRight   = _cameraTransform.right;   camRight.y   = 0f; camRight.Normalize();

            return (camForward * input.y + camRight * input.x).normalized;
        }

        // ─── Crouch ───────────────────────────────────────────────────────────

        private void ToggleCrouch()
        {
            if (_isInjured) return;
            _isCrouching = !_isCrouching;
            _targetCCHeight = _isCrouching ? _crouchHeight : _standHeight;
        }

        private void HandleCrouchTransition()
        {
            if (Mathf.Approximately(_cc.height, _targetCCHeight)) return;

            float newHeight = Mathf.MoveTowards(_cc.height, _targetCCHeight,
                _crouchTransitionSpeed * Time.deltaTime);
            float delta   = newHeight - _cc.height;
            _cc.height    = newHeight;
            _cc.center    = new Vector3(0f, newHeight * 0.5f, 0f);
            transform.position += Vector3.up * (delta * 0.5f);
        }

        // ─── Interact (Space) ─────────────────────────────────────────────────

        /// <summary>
        /// Space pressed. DBD priority order:
        ///   1. If a skill check is active → submit it immediately.
        ///   2. Otherwise → raycast for an IInteractable and call Interact().
        /// </summary>
        private void OnInteractPressed()
        {
            // Priority 1: Skill check
            var sc = SkillCheckManager.Instance;
            if (sc != null && sc.State.IsActive)
            {
                sc.Submit();
                return;
            }

            // Priority 2: Start/toggle interaction
            TryInteract();
        }

        /// <summary>Space released — end any held interaction.</summary>
        private void OnInteractReleased()
        {
            _isHoldingInteract = false;
            _currentHeldInteractable = null;
        }

        private void TryInteract()
        {
            Transform origin = _interactionOrigin != null ? _interactionOrigin : transform;

            if (Physics.Raycast(origin.position, origin.forward,
                out RaycastHit hit, _interactionRange, _interactionLayers))
            {
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    _isHoldingInteract       = true;
                    _currentHeldInteractable = interactable;
                    interactable.Interact(this);
                }
            }
        }

        // ─── Footsteps ────────────────────────────────────────────────────────

        private void TrackFootsteps()
        {
            if (!_isGrounded || !IsMoving) return;

            _distanceTravelled += _horizontalVelocity.magnitude * Time.deltaTime;
            if (_distanceTravelled < _footstepStride) return;

            _distanceTravelled = 0f;
            float intensity = IsSprinting   ? _footstepIntensity * 1.6f
                            : _isCrouching  ? _footstepIntensity * 0.4f
                                            : _footstepIntensity;

            EventBus.Publish(new SoundEmittedEvent
            {
                Position  = transform.position,
                Radius    = _footstepRadius,
                Intensity = intensity,
                Type      = SoundType.Footstep,
            });
        }

        // ─── Public API ───────────────────────────────────────────────────────

        public void SetCamera(Transform cam) => _cameraTransform = cam;
        public void SetInjured(bool injured)  => _isInjured = injured;

        public void SetInputEnabled(bool enabled)
        {
            if (enabled)
            {
                _moveAction?.Enable();
                _sprintAction?.Enable();
                _interactAction?.Enable();
            }
            else
            {
                _moveAction?.Disable();
                _sprintAction?.Disable();
                _interactAction?.Disable();
                _isHoldingInteract = false;
            }
        }

        // ─── Event Handlers ───────────────────────────────────────────────────

        private void OnHit(SurvivorHitEvent e)
        {
            if (e.SurvivorId != gameObject.GetInstanceID()) return;
            if (e.NewState == HitState.Injured) SetInjured(true);
        }

        private void OnHealed(SurvivorHealedEvent e)
        {
            if (e.SurvivorId != gameObject.GetInstanceID()) return;
            SetInjured(false);
        }

        // ─── Editor ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Transform origin = _interactionOrigin != null ? _interactionOrigin : transform;
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(origin.position, origin.forward * _interactionRange);
            Gizmos.DrawWireSphere(origin.position + origin.forward * _interactionRange, 0.08f);

            Gizmos.color = new Color(1f, 1f, 0f, 0.06f);
            Gizmos.DrawWireSphere(transform.position, _footstepRadius);
        }
#endif
    }
}
