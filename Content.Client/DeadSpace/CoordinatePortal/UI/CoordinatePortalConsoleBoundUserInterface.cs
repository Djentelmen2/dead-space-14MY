using Content.Shared.DeadSpace.CoordinatePortal;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.CoordinatePortal.UI;

[UsedImplicitly]
public sealed class CoordinatePortalConsoleBoundUserInterface : BoundUserInterface
{
    private CoordinatePortalConsoleWindow? _window;

    public CoordinatePortalConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CoordinatePortalConsoleWindow>();
        _window.SetTarget += (x, y) => SendMessage(new CoordinatePortalSetTargetMessage(x, y));
        _window.TogglePortal += (x, y) => SendMessage(new CoordinatePortalToggleMessage(x, y));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not CoordinatePortalConsoleBoundUserInterfaceState current)
            return;

        _window?.UpdateState(current);
    }
}
