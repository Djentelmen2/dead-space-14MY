namespace Content.Server.DeadSpace.CoordinatePortal.Components;

[RegisterComponent]
public sealed partial class CoordinatePortalDestinationComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid Console;
}
