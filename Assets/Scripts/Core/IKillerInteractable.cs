// ============================================================================
// IKillerInteractable.cs
// Namespace: Condemned.Gameplay
// Description: Interface for objects killers can interact with.
//              Examples: generators (kick), pallets (break), hooks (survivor hook).
// ============================================================================

using UnityEngine;

namespace Condemned.Gameplay
{
    /// <summary>
    /// Implement on objects that killers can interact with (Space/Interact action).
    /// </summary>
    public interface IKillerInteractable
    {
        /// <summary>
        /// Called when a killer interacts with this object.
        /// </summary>
        /// <param name="killer">The killer initiating the interaction.</param>
        void KillerInteract(KillerController killer);
    }
}
