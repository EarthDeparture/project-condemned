// ============================================================================
// CombatSystem.cs
// Namespace: Condemned.Systems
// Description: Resolves killer attack and lunge hits against survivors.
//              Subscribes to KillerAttackEvent and performs an OverlapSphere
//              to detect survivors within the killer's hit range, then fires
//              SurvivorHitEvent if a valid target is found.
//
//              Also handles the "pick up downed survivor" flow: when a killer
//              interacts with a Dying survivor, CombatSystem routes through
//              HookSystem.CarrySurvivor() to begin the carry state.
//
//              Hit state progression:
//                Healthy → SurvivorHitEvent(Injured)
//                Injured → SurvivorHitEvent(Dying)  → survivor downed
//
// No direct references to KillerController needed at the event level —
// only the KillerId (InstanceID) and attack metadata are required.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Condemned.Core;
using Condemned.Gameplay;

namespace Condemned.Systems
{
    [AddComponentMenu("Condemned/Systems/Combat System")]
    public class CombatSystem : Singleton<CombatSystem>
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Basic Attack")]
        [Tooltip("Hit detection radius for a standard attack (metres).")]
        [SerializeField] private float _basicAttackRange  = 2.0f;

        [Tooltip("Half-angle of the hit cone in front of the killer (degrees).")]
        [SerializeField] private float _attackConeAngle   = 60f;

        [Tooltip("Cooldown (seconds) between basic attacks.")]
        [SerializeField] private float _attackCooldown    = 0.85f;

        [Header("Lunge")]
        [Tooltip("Hit detection radius during a lunge (wider reach).")]
        [SerializeField] private float _lungeAttackRange  = 3.5f;

        [Header("Carry (downed survivor pickup)")]
        [Tooltip("Radius within which the killer can pick up a downed survivor.")]
        [SerializeField] private float _carryPickupRange  = 1.8f;

        [Header("Detection")]
        [Tooltip("LayerMask for survivor colliders. Set this to the Survivor layer.")]
        [SerializeField] private LayerMask _survivorLayer = ~0;

        // ─── State ────────────────────────────────────────────────────────────

        // Per-killer cooldown tracking: killerId → next-allowed attack time
        private readonly Dictionary<int, float> _cooldowns = new();

        // Cache of all registered killers (for position lookup by ID)
        private readonly Dictionary<int, KillerController> _killers = new();

        // ─── Lifecycle ────────────────────────────────────────────────────────

        protected override void OnInitialize()
        {
            EventBus.Subscribe<KillerAttackEvent>(OnKillerAttack);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<KillerAttackEvent>(OnKillerAttack);
        }

        // ─── Registration ─────────────────────────────────────────────────────

        /// <summary>Register the killer controller so we can look up its position by ID.</summary>
        public void RegisterKiller(KillerController killer)
        {
            _killers[killer.gameObject.GetInstanceID()] = killer;
        }

        // ─── Attack Resolution ────────────────────────────────────────────────

        private void OnKillerAttack(KillerAttackEvent e)
        {
            // Lazy-discover the killer if not yet registered
            if (!_killers.TryGetValue(e.KillerId, out var killer))
            {
                var found = FindFirstObjectByType<KillerController>();
                if (found != null && found.gameObject.GetInstanceID() == e.KillerId)
                {
                    RegisterKiller(found);
                    killer = found;
                }
                else return;
            }

            // Cooldown check (lunge bypasses the basic attack cooldown)
            if (!e.IsLunge)
            {
                _cooldowns.TryGetValue(e.KillerId, out float nextAllowed);
                if (Time.time < nextAllowed) return;
                _cooldowns[e.KillerId] = Time.time + _attackCooldown;
            }

            float range = e.IsLunge ? _lungeAttackRange : _basicAttackRange;

            ResolveHit(killer, range, e.IsLunge);
        }

        private void ResolveHit(KillerController killer, float range, bool isLunge)
        {
            var hits = Physics.OverlapSphere(killer.transform.position, range, _survivorLayer);

            foreach (var col in hits)
            {
                var survivor = col.GetComponentInParent<SurvivorController>();
                if (survivor == null) continue;

                // Cone check (skip for lunge — wider arc)
                if (!isLunge && !InAttackCone(killer, survivor.transform.position)) continue;

                int       survivorId   = survivor.gameObject.GetInstanceID();
                var       healthSystem = SurvivorHealthSystem.Instance;
                HitState  current      = healthSystem?.GetHealthState(survivorId) ?? HitState.Healthy;

                // Already dead/sacrificed — check for carry instead
                if (current == HitState.Dying)
                {
                    TryCarry(killer, survivor);
                    continue;
                }

                HitState next = current == HitState.Healthy ? HitState.Injured : HitState.Dying;

                EventBus.Publish(new SurvivorHitEvent
                {
                    SurvivorId  = survivorId,
                    NewState    = next,
                    HitPosition = survivor.transform.position,
                });

#if DEBUG || DEVELOPMENT_BUILD
                Debug.Log($"[CombatSystem] Survivor hit: {current} → {next}");
#endif

                // Only hit one survivor per swing (first valid target wins)
                return;
            }
        }

        // ─── Carry Pickup ─────────────────────────────────────────────────────

        /// <summary>
        /// Called when the killer attacks a Dying (downed) survivor.
        /// If in range, begins carry via HookSystem.
        /// </summary>
        private void TryCarry(KillerController killer, SurvivorController survivor)
        {
            if (HookSystem.Instance == null) return;
            if (HookSystem.Instance.IsCarrying(killer)) return;

            float dist = Vector3.Distance(killer.transform.position, survivor.transform.position);
            if (dist > _carryPickupRange) return;

            HookSystem.Instance.CarrySurvivor(killer, survivor);
            killer.SetCarrying(true);

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log("[CombatSystem] Killer picked up downed survivor.");
#endif
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private bool InAttackCone(KillerController killer, Vector3 targetPos)
        {
            Vector3 toTarget = (targetPos - killer.transform.position).normalized;
            toTarget.y       = 0f;

            float   angle = Vector3.Angle(killer.transform.forward, toTarget);
            return angle <= _attackConeAngle;
        }

        // ─── Editor ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            foreach (var killer in _killers.Values)
            {
                if (killer == null) continue;

                // Attack range
                Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
                Gizmos.DrawWireSphere(killer.transform.position, _basicAttackRange);

                // Lunge range
                Gizmos.color = new Color(1f, 0.4f, 0f, 0.08f);
                Gizmos.DrawWireSphere(killer.transform.position, _lungeAttackRange);
            }
        }
#endif
    }
}
