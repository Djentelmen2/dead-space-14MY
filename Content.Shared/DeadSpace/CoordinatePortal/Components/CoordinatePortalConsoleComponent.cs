using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.DeadSpace.CoordinatePortal.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CoordinatePortalConsoleComponent : Component
{
    [DataField]
    public string PortalPrototype = "RndCoordinatePortal";

    [DataField]
    public int TargetX;

    [DataField]
    public int TargetY;

    [DataField]
    public float MaxCoordinateDistance = 3048f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(3);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextReady;

    [DataField]
    public SoundSpecifier OpenSound = new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg")
    {
        Params = AudioParams.Default.WithVolume(-2f)
    };

    [DataField]
    public SoundSpecifier CloseSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");

    [DataField]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ActivePortal;
}
