// ============================================================================
// SurvivorController.cs
// Namespace: Condemned.Gameplay
// Description: Survivor character controller. Handles movement, sprint, crouch,
//              interaction raycasting, and footstep sound events.
//              Uses CharacterController (not Rigidbody) for precise control.
//
// Scene Setup:
//   1. Add to the Survivor prefab root GameObject
//   2. Ensure a CharacterController component is also on the root
//   3. Assign _inputActions (CondemendInputActions asset in Assets/Settings/)
//   4. Assign _cameraTransform (Main Camera transform)
//   5. Assign _interactionOrigin (an empty child transform at eye level)
// ============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Condemned.Core;

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
        [Tooltip("How quickly the character reaches target speed.")]
        [SerializeField, Range(4f, 30f)] private float _acceleration  = 14f;
        [Tooltip("How quickly the character stops when no input is given.")]
        [SerializeField, Range(4f, 30f)] private float _deceleration  = 20f;
        [Tooltip("Multiplier applied to movement speed when injured.")]
        [SerializeField, Range(0.4f, 1f)] private float _injuredSpeedMultiplier = 0.78f;

        [Header("Crouch")]
        [SerializeField] private float _standHeight  = 1.8f;
        [SerializeField] private float _crouchHeight = 1.0f;
        [SerializeField] private float _crouchTransitionSpeed = 8f;

        [Header("Gravity")]
        [SerializeField] private float _gravity          = -18f;
        [SerializeField] private float _groundedGravity  = -2f;

        [Header("Camera")]
        [Tooltip("The Main Camera transform — used to resolve isometric move direction.")]
        [SerializeField] private Transform _cameraTransform;

        [Header("Interaction")]
        [Tooltip("Empty child transform at eye level — origin of interaction raycasts.")]
        [SerializeField] private Transform _interactionOrigin;
        [SerializeField] private float     _interactionRange  = 2.5f;
        [SerializeField] private LayerMask _interactionLayers = ~0;

        [Header("Footsteps")]
        [Tooltip("Distance travelled (m) between footstep events.")]
        [SerializeField] private float _footstepStride    = 2.0f;
        [Tooltip("Sound event radius for survivor footsteps.")]
        [SerializeField] private float _footstepRadius    = 12f;
        [Tooltip("Sound event intensity (0–1). Lower than killer footsteps.")]
        [SerializeField, Range(0f, 1f)] private float _footstepIntensity = 0.25f;

        // ─── Input Actions ────────────────────────────────────────────────────

        private InputAction _moveAction;
        private InputAction _sprintAction;
        private InputAction _crouchAction;
        private InputAction _interactAction;

        // ─── Components & References ──────────────────────────────────────────

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

        // ─── Properties ───────────────────────────────────────────────────────

        public bool IsMoving    => _horizontalVelocity.magnitude > 0.1f;
        public bool IsSprinting => _isSprinting && IsMoving;
        public bool IsCrouching => _isCrouching;
        public bool IsGrounded  => _isGrounded;

        // ─── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _targetCCHeight = _standHeight;

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
                Debug.LogError("[SurvivorController] InputActionAsset not assigned. Assign CondemendInputActions in the Inspector.");
                return;
            }

            var map = _inputActions.FindActionMap("Survivor", throwIfNotFound: true);

            _moveAction     = map.FindAction("Move",     throwIfNotFound: true);
            _sprintAction   = map.FindAction("Sprint",   throwIfNotFound: true);
            _interactAction = map.FindAction("Interact", throwIfNotFound: true);

            _crouchAction = map.FindAction("Crouch", throwIfNotFound: true);
            _crouchAction.performed += _ => ToggleCrouch();

            _interactAction.performed += _ => TryInteract();
        }

        // ─── Ground Check ─────────────────────────────────────────────────────

        private void CheckGrounded()
        {
            _isGrounded = _cc.isGrounded;
        }

        // ─── Movement ─────────────────────────────────────────────────────────

        private void HandleMovement()
        {
            Vector2 input = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;

            // Resolve move direction relative to isometric camera
            Vector3 moveDir = ResolveMovementDirection(input);

            // Determine target speed
            _isSprinting = (_sprintAction?.IsPressed() ?? false) && !_isCrouching && !_isInjured;

            float targetSpeed = _isCrouching ? _crouchSpeed
                              : _isSprinting ? _sprintSpeed
                              : _walkSpeed;

            if (_isInjured) targetSpeed *= _injuredSpeedMultiplier;

            // Smooth acceleration / deceleration
            float smoothRate = moveDir.magnitude > 0.01f ? _acceleration : _deceleration;
            Vector3 targetVelocity = moveDir * targetSpeed;
            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity, targetVelocity, smoothRate * Time.deltaTime);

            // Rotate character to face movement direction
            if (_horizontalVelocity.magnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_horizontalVelocity, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 16f * Time.deltaTime);
            }

            // Vertical velocity (gravity)
            _verticalVelocity = _isGrounded
                ? _groundedGravity
                : _verticalVelocity + _gravity * Time.deltaTime;

            // Move
            Vector3 motion = _horizontalVelocity + Vector3.up * _verticalVelocity;
            _cc.Move(motion * Time.deltaTime);
        }

        /// <summary>
        /// Project camera forward/right onto the horizontal plane and resolve
        /// a world-space movement direction from 2D input.
        /// </summary>
        private Vector3 ResolveMovementDirection(Vector2 input)
        {
            if (_cameraTransform == null || input.magnitude < 0.01f)
                return Vector3.zero;

            Vector3 camForward = _cameraTransform.forward;
            Vector3 camRight   = _cameraTransform.right;

            camForward.y = 0f; camForward.Normalize();
            camRight.y   = 0f; camRight.Normalize();

            return (camForward * input.y + camRight * input.x).normalized;
        }

        // ─── Crouch ───────────────────────────────────────────────────────────

        private void ToggleCrouch()
        {
            if (_isInjured) return; // Cannot crouch while injured — use crawl state

            _isCrouching = !_isCrouching;
            _targetCCHeight = _isCrouching ? _crouchHeight : _standHeight;
        }

        private void HandleCrouchTransition()
        {
            if (Mathf.Approximately(_cc.height, _targetCCHeight)) return;

            float newHeight = Mathf.MoveTowards(_cc.height, _targetCCHeight,
                _crouchTransitionSpeed * Time.deltaTime);

            // Adjust center so feet stay on the ground as height changes
            float delta  = newHeight - _cc.height;
            _cc.height   = newHeight;
            _cc.center   = new Vector3(0f, newHeight * 0.5f, 0f);

            // Compensate position so the character doesn't sink into the floor
            transform.position += Vector3.up * (delta * 0.5f);
        }

        // ─── Footsteps ────────────────────────────────────────────────────────

        private void TrackFootsteps()
        {
            if (!_isGrounded || !IsMoving) return;

            _distanceTravelled += _horizontalVelocity.magnitude * Time.deltaTime;

            if (_distanceTravelled >= _footstepStride)
            {
                _distanceTravelled = 0f;
                EmitFootstepEvent();
            }
        }

        private void EmitFootstepEvent()
        {
            float intensity = IsSprinting
                ? _footstepIntensity * 1.6f   // Sprinting is louder
                : _isCrouching
                    ? _footstepIntensity * 0.4f // Crouch is quieter
                    : _footstepIntensity;

            EventBus.Publish(new SoundEmittedEvent
            {
                Position  = transform.position,
                Radius    = _footstepRadius,
                Intensity = intensity,
                Type      = SoundType.Footstep,
            });
        }

        // ─── Interaction ──────────────────────────────────────────────────────

        private void TryInteract()
        {
            Transform origin = _interactionOrigin != null ? _interactionOrigin : transform;

            if (Physics.Raycast(origin.position, origin.forward,
                out RaycastHit hit, _interactionRange, _interactionLayers))
            {
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                interactable?.Interact(this);
            }
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>Externally set the camera transform (called on spawn).</summary>
        public void SetCamera(Transform cameraTransform) => _cameraTransform = cameraTransform;

        /// <summary>Force the survivor into or out of the injured movement state.</summary>
        public void SetInjured(bool injured) => _isInjured = injured;

        /// <summary>Stop all movement input processing (e.g. while hooked or carried).</summary>
        public void SetInputEnabled(bool enabled)
        {
            if (enabled) { _moveAction?.Enable(); _sprintAction?.Enable(); }
            else         { _moveAction?.Disable(); _sprintAction?.Disable(); }
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
            // Interaction range
            Transform origin = _interactionOrigin != null ? _interactionOrigin : transform;
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(origin.position, origin.forward * _interactionRange);
            Gizmos.DrawWireSphere(origin.position + origin.forward * _interactionRange, 0.08f);

            // Footstep radius
            Gizmos.color = new Color(1f, 1f, 0f, 0.06f);
            Gizmos.DrawWireSphere(transform.position, _footstepRadius);
        }
#endif
    }

    // ─── Interactable Interface ───────────────────────────────────────────────

    /// <summary>
    /// Implement this on any object a survivor can interact with
    /// (generators, pallets, lockers, hooks, exit gates).
    /// </summary>
    public interface IInteractable
    {
        void Interact(SurvivorController survivor);
    }
}
