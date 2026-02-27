// ============================================================================
// GameEvents.cs
// Namespace: Condemned.Core
// Description: Centralized definitions for all EventBus event types.
//              Add new events here. Keep structs lightweight — value types only.
// ============================================================================

using UnityEngine;

namespace Condemned.Core
{
    // ─── Match Lifecycle ──────────────────────────────────────────────────────

    public struct MatchStartedEvent { }
    public struct MatchEndedEvent
    {
        public bool KillerWon;
        public int SurvivorsEscaped;
        public int SurvivorsSacrificed;
    }

    // ─── Generator Events ─────────────────────────────────────────────────────

    public struct GenRepairedEvent
    {
        public int GenIndex;
        public int TotalCompleted;
    }

    public struct GenProgressChangedEvent
    {
        public int   GenIndex;
        public float Progress; // 0–100
    }

    public struct GenRegressionStartedEvent { public int GenIndex; }
    public struct GenKickedEvent            { public int GenIndex; public Vector3 Position; }

    // ─── Exit Gate Events ─────────────────────────────────────────────────────

    public struct ExitGatesActivatedEvent { }
    public struct ExitGateOpenedEvent     { public int GateIndex; }

    // ─── Survivor State Events ────────────────────────────────────────────────

    public struct SurvivorHitEvent
    {
        public int SurvivorId;
        public HitState NewState; // Injured or Dying
        public Vector3 HitPosition;
    }

    public struct SurvivorDownedEvent   { public int SurvivorId; }
    public struct SurvivorCarriedEvent  { public int SurvivorId; public int KillerId; }
    public struct SurvivorHookedEvent   { public int SurvivorId; public int HookId; public int HookStage; }
    public struct SurvivorUnhookedEvent { public int SurvivorId; public int RescuerId; }
    public struct SurvivorSacrificedEvent { public int SurvivorId; }
    public struct SurvivorEscapedEvent    { public int SurvivorId; }
    public struct SurvivorHealedEvent     { public int SurvivorId; }

    // ─── Killer Events ────────────────────────────────────────────────────────

    public struct KillerAttackEvent   { public int KillerId; public bool IsLunge; }
    public struct KillerStunnedEvent  { public int KillerId; public float StunDuration; }
    public struct BloodlustTierChangedEvent { public int KillerId; public int Tier; } // 0 = reset

    // ─── Chase Events ─────────────────────────────────────────────────────────

    public struct ChaseStartedEvent { public int KillerId; public int SurvivorId; }
    public struct ChaseEndedEvent   { public int KillerId; public int SurvivorId; }

    // ─── Hatch Events ─────────────────────────────────────────────────────────

    public struct HatchSpawnedEvent  { public Vector3 Position; }
    public struct HatchOpenedEvent   { public int SurvivorId; }
    public struct HatchClosedEvent   { }

    // ─── Perk Events ──────────────────────────────────────────────────────────

    public struct PerkActivatedEvent { public int OwnerId; public string PerkId; }

    // ─── Sound Events ─────────────────────────────────────────────────────────

    /// <summary>
    /// Emitted by any action that produces in-world sound.
    /// AI bots subscribe to this for awareness.
    /// </summary>
    public struct SoundEmittedEvent
    {
        public Vector3 Position;
        public float   Radius;
        public float   Intensity; // 0–1, used for AI priority weighting
        public SoundType Type;
    }

    // ─── Enums ────────────────────────────────────────────────────────────────

    public enum HitState     { Healthy, Injured, Dying }
    public enum SoundType    { Footstep, GenRepair, Vault, PalletDrop, LockerUse, Injured, Hook }
    public enum GameState    { Idle, WarmUp, InProgress, Ended }
    public enum HookStage    { None, StageOne, StageTwo, Sacrificed }
    public enum KillerRole   { Killer }
    public enum SurvivorRole { Survivor }
}
