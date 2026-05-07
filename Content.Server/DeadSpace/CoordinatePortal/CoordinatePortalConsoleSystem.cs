using System.Numerics;
using Content.Server.DeadSpace.CoordinatePortal.Components;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared.Access.Systems;
using Content.Shared.DeadSpace.CoordinatePortal;
using Content.Shared.DeadSpace.CoordinatePortal.Components;
using Content.Shared.Power;
using Content.Shared.Popups;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.CoordinatePortal;

public sealed class CoordinatePortalConsoleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly LinkedEntitySystem _link = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CoordinatePortalConsoleComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CoordinatePortalConsoleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CoordinatePortalConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CoordinatePortalConsoleComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<CoordinatePortalConsoleComponent, CoordinatePortalSetTargetMessage>(OnSetTarget);
        SubscribeLocalEvent<CoordinatePortalConsoleComponent, CoordinatePortalToggleMessage>(OnToggle);

        SubscribeLocalEvent<CoordinatePortalDestinationComponent, ComponentShutdown>(OnDestinationShutdown);
    }

    private void OnStartup(EntityUid uid, CoordinatePortalConsoleComponent component, ComponentStartup args)
    {
        UpdateAppearance(uid, false);
    }

    private void OnShutdown(EntityUid uid, CoordinatePortalConsoleComponent component, ComponentShutdown args)
    {
        ClosePortal(uid, component, false);
    }

    private void OnUiOpened(EntityUid uid, CoordinatePortalConsoleComponent component, BoundUIOpenedEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    private void OnPowerChanged(EntityUid uid, CoordinatePortalConsoleComponent component, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            ClosePortal(uid, component);

        UpdateUserInterface(uid, component);
    }

    private void OnSetTarget(EntityUid uid, CoordinatePortalConsoleComponent component, CoordinatePortalSetTargetMessage args)
    {
        if (!CanUse(uid, component, args.Actor))
            return;

        if (IsPortalActive(uid, component))
            return;

        if (!TrySetTarget(uid, component, args.X, args.Y, args.Actor))
            return;

        _popup.PopupEntity(Loc.GetString("coordinate-portal-popup-coordinates-saved"),
            uid,
            args.Actor,
            PopupType.Medium);

        UpdateUserInterface(uid, component);
    }

    private void OnToggle(EntityUid uid, CoordinatePortalConsoleComponent component, CoordinatePortalToggleMessage args)
    {
        if (!CanUse(uid, component, args.Actor))
            return;

        if (IsPortalActive(uid, component))
        {
            ClosePortal(uid, component);
            return;
        }

        if (!TrySetTarget(uid, component, args.X, args.Y, args.Actor))
            return;

        OpenPortal(uid, component, args.Actor);
    }

    private void OnDestinationShutdown(EntityUid uid, CoordinatePortalDestinationComponent component, ComponentShutdown args)
    {
        if (!TryComp<CoordinatePortalConsoleComponent>(component.Console, out var console) ||
            console.ActivePortal != uid)
        {
            return;
        }

        ClosePortal(component.Console, console, true, false);
    }

    private bool CanUse(EntityUid uid, CoordinatePortalConsoleComponent component, EntityUid user)
    {
        if (!_power.IsPowered(uid))
        {
            Deny(uid, user, "coordinate-portal-popup-unpowered", component);
            return false;
        }

        if (!_accessReader.IsAllowed(user, uid))
        {
            Deny(uid, user, "coordinate-portal-popup-access-denied", component);
            return false;
        }

        return true;
    }

    private bool TrySetTarget(EntityUid uid, CoordinatePortalConsoleComponent component, int x, int y, EntityUid user)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y))
        {
            Deny(uid, user, "coordinate-portal-popup-invalid-coordinates", component);
            return false;
        }

        if (!TryGetCurrentMapCoordinates(uid, out var current))
        {
            Deny(uid, user, "coordinate-portal-popup-no-grid", component);
            return false;
        }

        component.TargetX = ClampTargetCoordinate(x, current.Position.X, component.MaxCoordinateDistance);
        component.TargetY = ClampTargetCoordinate(y, current.Position.Y, component.MaxCoordinateDistance);
        return true;
    }

    private void OpenPortal(EntityUid uid, CoordinatePortalConsoleComponent component, EntityUid user)
    {
        var nextReady = _metadata.GetPauseTime(uid) + component.NextReady;
        if (_timing.CurTime < nextReady)
        {
            Deny(uid, user, "coordinate-portal-popup-cooldown", component);
            return;
        }

        if (!TryGetTargetCoordinates(uid, component, out var target))
        {
            Deny(uid, user, "coordinate-portal-popup-no-grid", component);
            return;
        }

        if (_lookup.AnyEntitiesIntersecting(target, LookupFlags.Static))
        {
            Deny(uid, user, "coordinate-portal-popup-blocked", component);
            return;
        }

        ClosePortal(uid, component, false);

        var destination = Spawn(component.PortalPrototype, target);
        var destinationMarker = EnsureComp<CoordinatePortalDestinationComponent>(destination);
        destinationMarker.Console = uid;

        var sourcePortal = EnsureComp<PortalComponent>(uid);
        sourcePortal.RandomTeleport = false;
        sourcePortal.CanTeleportToOtherMaps = true;
        Dirty(uid, sourcePortal);

        var destinationPortal = EnsureComp<PortalComponent>(destination);
        destinationPortal.RandomTeleport = false;
        destinationPortal.CanTeleportToOtherMaps = true;
        Dirty(destination, destinationPortal);

        // Штатный PortalTimeoutComponent не дает объекту сразу зациклиться между двумя связанными порталами.
        _link.TryLink(uid, destination);

        component.ActivePortal = destination;
        UpdateAppearance(uid, true);
        _audio.PlayPvs(component.OpenSound, uid);
        _audio.PlayPvs(component.OpenSound, destination);
        UpdateUserInterface(uid, component);
    }

    private void ClosePortal(EntityUid uid,
        CoordinatePortalConsoleComponent component,
        bool startCooldown = true,
        bool deleteDestination = true)
    {
        var destination = component.ActivePortal;
        var wasActive = destination != null || HasComp<PortalComponent>(uid);

        component.ActivePortal = null;
        RemComp<PortalComponent>(uid);
        RemComp<LinkedEntityComponent>(uid);
        UpdateAppearance(uid, false);

        if (deleteDestination && destination != null && !Deleted(destination.Value))
            QueueDel(destination.Value);

        if (startCooldown && wasActive)
        {
            component.NextReady = _timing.CurTime + component.Cooldown;
            _audio.PlayPvs(component.CloseSound, uid);
        }

        UpdateUserInterface(uid, component);
    }

    private bool IsPortalActive(EntityUid uid, CoordinatePortalConsoleComponent component)
    {
        if (component.ActivePortal == null)
            return false;

        if (!Deleted(component.ActivePortal.Value) && HasComp<PortalComponent>(uid))
            return true;

        ClosePortal(uid, component, false, false);
        return false;
    }

    private bool TryGetTargetCoordinates(EntityUid uid,
        CoordinatePortalConsoleComponent component,
        out MapCoordinates coordinates)
    {
        coordinates = MapCoordinates.Nullspace;

        if (!TryGetCurrentMapCoordinates(uid, out var current))
            return false;

        coordinates = new MapCoordinates(new Vector2(component.TargetX, component.TargetY), current.MapId);
        return true;
    }

    private (float X, float Y, bool HasParent) GetCurrentCoordinates(EntityUid uid)
    {
        if (!TryGetCurrentMapCoordinates(uid, out var coordinates))
            return (0f, 0f, false);

        return (coordinates.Position.X, coordinates.Position.Y, true);
    }

    private bool TryGetCurrentMapCoordinates(EntityUid uid, out MapCoordinates coordinates)
    {
        coordinates = _transform.GetMapCoordinates(uid, Transform(uid));
        return coordinates.MapId != MapId.Nullspace;
    }

    private static int ClampTargetCoordinate(int value, float current, float maxDistance)
    {
        var clamped = Math.Clamp(value, current - maxDistance, current + maxDistance);
        return (int) MathF.Round(clamped, MidpointRounding.AwayFromZero);
    }

    private void UpdateUserInterface(EntityUid uid, CoordinatePortalConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        var current = GetCurrentCoordinates(uid);
        var state = new CoordinatePortalConsoleBoundUserInterfaceState(
            component.TargetX,
            component.TargetY,
            current.X,
            current.Y,
            IsPortalActive(uid, component),
            _power.IsPowered(uid),
            current.HasParent,
            component.NextReady,
            component.Cooldown);

        _ui.SetUiState(uid, CoordinatePortalConsoleUiKey.Key, state);
    }

    private void UpdateAppearance(EntityUid uid, bool active)
    {
        _appearance.SetData(uid, CoordinatePortalConsoleVisuals.Active, active);
    }

    private void Deny(EntityUid uid, EntityUid user, string message, CoordinatePortalConsoleComponent component)
    {
        _popup.PopupEntity(Loc.GetString(message), uid, user, PopupType.MediumCaution);
        _audio.PlayPvs(component.DenySound, uid);
    }
}
