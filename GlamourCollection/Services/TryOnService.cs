namespace Main.Services;

public interface ITryOnService
{
    bool CanTryOn(uint itemId);

    void TryOn(uint itemId);
}

public sealed class TryOnService : ITryOnService
{
    public bool CanTryOn(uint itemId) => false;

    public void TryOn(uint itemId)
    {
        // TODO: Implement through the game's Try On / Fitting Room flow in a later phase.
    }
}
