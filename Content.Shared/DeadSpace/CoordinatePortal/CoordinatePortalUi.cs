using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.CoordinatePortal;

[Serializable, NetSerializable]
public enum CoordinatePortalConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum CoordinatePortalConsoleVisuals : byte
{
    Active
}

[Serializable, NetSerializable]
public enum CoordinatePortalConsoleVisualLayers : byte
{
    Portal
}

[Serializable, NetSerializable]
public sealed class CoordinatePortalConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly int TargetX;
    public readonly int TargetY;
    public readonly float CurrentX;
    public readonly float CurrentY;
    public readonly bool Active;
    public readonly bool Powered;
    public readonly bool HasCoordinateParent;
    public readonly TimeSpan NextReady;
    public readonly TimeSpan Cooldown;

    public CoordinatePortalConsoleBoundUserInterfaceState(
        int targetX,
        int targetY,
        float currentX,
        float currentY,
        bool active,
        bool powered,
        bool hasCoordinateParent,
        TimeSpan nextReady,
        TimeSpan cooldown)
    {
        TargetX = targetX;
        TargetY = targetY;
        CurrentX = currentX;
        CurrentY = currentY;
        Active = active;
        Powered = powered;
        HasCoordinateParent = hasCoordinateParent;
        NextReady = nextReady;
        Cooldown = cooldown;
    }
}

[Serializable, NetSerializable]
public sealed class CoordinatePortalSetTargetMessage : BoundUserInterfaceMessage
{
    public readonly int X;
    public readonly int Y;

    public CoordinatePortalSetTargetMessage(int x, int y)
    {
        X = x;
        Y = y;
    }
}

[Serializable, NetSerializable]
public sealed class CoordinatePortalToggleMessage : BoundUserInterfaceMessage
{
    public readonly int X;
    public readonly int Y;

    public CoordinatePortalToggleMessage(int x, int y)
    {
        X = x;
        Y = y;
    }
}
