// ============================================================================
// IInteractable.cs
// Namespace: Condemned.Gameplay
// Description: Interface for objects survivors can interact with via Space bar.
//              Examples: generators, pallets, vaults, lockers, hooks, hatch.
//
// Usage:
//   public class SomeObject : MonoBehaviour, IInteractable
//   {
//       public void Interact(SurvivorController survivor) { ... }
//   }
// ============================================================================

using UnityEngine;

namespace Condemned.Gameplay
{
    /// <summary>
    /// Implement on objects that survivors can interact with (Space bar).
    /// The interaction is triggered from SurvivorController via raycast.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Called when a survivor presses Space near this object.
        /// </summary>
        /// <param name="survivor">The survivor initiating the interaction.</param>
        void Interact(SurvivorController survivor);
    }
}
