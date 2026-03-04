public interface IInteractable
{
    bool CanInteract(PlayerMovement player);
    void OnInteractStart(PlayerMovement player);
    void OnInteractHeld(PlayerMovement player, float deltaTime);
    void OnInteractEnd(PlayerMovement player);
    string GetInteractLabel();
}
