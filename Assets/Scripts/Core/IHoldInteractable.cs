// ============================================================================
// IHoldInteractable.cs
// Namespace: Condemned.Gameplay
// Description: Interface for interactions that require holding Space (e.g., healing).
//              The interactable polls SurvivorController.IsHoldingInteract each frame
//              to track continuous press state.
// ============================================================================

namespace Condemned.Gameplay
{
    /// <summary>
    /// Implement on objects that require holding Space (e.g., healing interactions).
    /// These objects poll SurvivorController.IsHoldingInteract each frame.
    /// </summary>
    public interface IHoldInteractable
    {
        // No methods needed - polls SurvivorController.IsHoldingInteract directly
    }
}
