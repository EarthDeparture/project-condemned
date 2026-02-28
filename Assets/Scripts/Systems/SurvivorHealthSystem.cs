// ============================================================================
// SurvivorHealthSystem.cs
// Namespace: Condemned.Systems
// Description: Singleton that tracks health state for every registered
//              survivor. Health state flows: Healthy → Injured → Dying.
//              Dying survivors are on the ground (downed); hook/sacrifice
//              transitions are handled by HookSystem.
//
//              This system is the authoritative source for survivor health —
//              SurvivorController delegates visual/movement effects here
//              via EventBus rather than managing state itself.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Condemned.Core;
using Condemned.Gameplay;

namespace Condemned.Systems
{
    [AddComponentMenu("Condemned/Systems/Survivor Health System")]
    public class SurvivorHealthSystem : Singleton<SurvivorHealthSystem>
    {
        // ─── Per-Survivor Health Record ───────────────────────────────────────

        public class SurvivorHealth
        {
            public SurvivorController Controller;
            public HitState           State = HitState.Healthy;
            public HookStage          HookStage = HookStage.None;
        }

        // ─── State ────────────────────────────────────────────────────────────

        private readonly Dictionary<int, SurvivorHealth> _records = new();

        // ─── Lifecycle ────────────────────────────────────────────────────────

        protected override void OnInitialize()
        {
            EventBus.Subscribe<SurvivorHitEvent>(OnHit);
            EventBus.Subscribe<SurvivorHealedEvent>(OnHealed);
            EventBus.Subscribe<SurvivorHookedEvent>(OnHooked);
            EventBus.Subscribe<SurvivorUnhookedEvent>(OnUnhooked);
            EventBus.Subscribe<SurvivorSacrificedEvent>(OnSacrificed);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<SurvivorHitEvent>(OnHit);
            EventBus.Unsubscribe<SurvivorHealedEvent>(OnHealed);
            EventBus.Unsubscribe<SurvivorHookedEvent>(OnHooked);
            EventBus.Unsubscribe<SurvivorUnhookedEvent>(OnUnhooked);
            EventBus.Unsubscribe<SurvivorSacrificedEvent>(OnSacrificed);
        }

        // ─── Registration ─────────────────────────────────────────────────────

        public void RegisterSurvivor(SurvivorController survivor)
        {
            int id = survivor.gameObject.GetInstanceID();
            if (!_records.ContainsKey(id))
                _records[id] = new SurvivorHealth { Controller = survivor };
        }

        public void UnregisterSurvivor(SurvivorController survivor)
        {
            _records.Remove(survivor.gameObject.GetInstanceID());
        }

        // ─── Queries ──────────────────────────────────────────────────────────

        public HitState  GetHealthState(int survivorId) =>
            _records.TryGetValue(survivorId, out var r) ? r.State : HitState.Healthy;

        public HookStage GetHookStage(int survivorId) =>
            _records.TryGetValue(survivorId, out var r) ? r.HookStage : HookStage.None;

        public bool IsDying(int survivorId) =>
            GetHealthState(survivorId) == HitState.Dying;

        public SurvivorHealth GetRecord(int survivorId) =>
            _records.TryGetValue(survivorId, out var r) ? r : null;

        // ─── Event Handlers ───────────────────────────────────────────────────

        private void OnHit(SurvivorHitEvent e)
        {
            if (!_records.TryGetValue(e.SurvivorId, out var record)) return;

            var prev = record.State;
            record.State = e.NewState;

            if (record.State == HitState.Dying)
            {
                record.Controller.SetInputEnabled(false);
                EventBus.Publish(new SurvivorDownedEvent { SurvivorId = e.SurvivorId });

#if DEBUG || DEVELOPMENT_BUILD
                Debug.Log($"[HealthSystem] Survivor {e.SurvivorId} downed.");
#endif
            }
            else if (record.State == HitState.Injured)
            {
                record.Controller.SetInjured(true);
#if DEBUG || DEVELOPMENT_BUILD
                Debug.Log($"[HealthSystem] Survivor {e.SurvivorId} injured.");
#endif
            }
        }

        private void OnHealed(SurvivorHealedEvent e)
        {
            if (!_records.TryGetValue(e.SurvivorId, out var record)) return;

            if (record.State == HitState.Injured)
            {
                record.State = HitState.Healthy;
                record.Controller.SetInjured(false);
#if DEBUG || DEVELOPMENT_BUILD
                Debug.Log($"[HealthSystem] Survivor {e.SurvivorId} healed.");
#endif
            }
        }

        private void OnHooked(SurvivorHookedEvent e)
        {
            if (!_records.TryGetValue(e.SurvivorId, out var record)) return;
            record.HookStage = (HookStage)e.HookStage;
        }

        private void OnUnhooked(SurvivorUnhookedEvent e)
        {
            if (!_records.TryGetValue(e.SurvivorId, out var record)) return;
            record.HookStage = HookStage.None;
            record.State     = HitState.Injured;
            record.Controller.SetInputEnabled(true);
            record.Controller.SetInjured(true);
        }

        private void OnSacrificed(SurvivorSacrificedEvent e)
        {
            if (!_records.TryGetValue(e.SurvivorId, out var record)) return;
            record.HookStage = HookStage.Sacrificed;
            record.State     = HitState.Dying;
            record.Controller.SetInputEnabled(false);
        }
    }
}
