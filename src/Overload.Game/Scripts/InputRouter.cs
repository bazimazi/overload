using Godot;
using Overload.Domain;

namespace Overload.Game;

public readonly record struct PlayerIntent(Vector2 Move, Vector2 Aim, SkillId? Action, bool PreserveMemories = false);

public sealed class InputRouter
{
    public static readonly string[] Names = ["move_left", "move_right", "move_up", "move_down", "cleave", "pulse", "lance", "special", "evade", "flask", "pause", "interact", "local_map", "region_map", "preserve", "track", "move_to", "character", "inventory"];
    private readonly string settingsPath;
    private SkillId? buffered;
    private SkillId? pressed;
    private long expires;
    private Vector2 lastAim = Vector2.Right;
    private Vector2 lastMouse;
    private bool pointerObserved;
    public bool Controller { get; private set; }
    public Vector2 PointerPosition { get; private set; }
    public bool PreservingMemories => Input.IsActionPressed("preserve");
    public InputRouter(bool smoke)
    {
        settingsPath = smoke ? "user://tests/controls.cfg" : "user://controls.cfg";
        Key[] keys = [Key.A, Key.D, Key.W, Key.S, Key.None, Key.Q, Key.E, Key.R, Key.Space, Key.F, Key.Escape, Key.G, Key.Tab, Key.M, Key.Shift, Key.T, Key.None, Key.C, Key.I];
        for (var i = 0; i < Names.Length; i++)
        {
            if (!InputMap.HasAction(Names[i])) InputMap.AddAction(Names[i], 0.2f);
            if (keys[i] != Key.None) InputMap.ActionAddEvent(Names[i], new InputEventKey { PhysicalKeycode = keys[i] });
        }
        InputMap.ActionAddEvent("cleave", new InputEventMouseButton { ButtonIndex = MouseButton.Left });
        InputMap.ActionAddEvent("move_to", new InputEventMouseButton { ButtonIndex = MouseButton.Right });
        AddButton("cleave", JoyButton.RightShoulder); AddButton("pulse", JoyButton.X); AddButton("lance", JoyButton.Y);
        AddButton("special", JoyButton.B);
        AddButton("evade", JoyButton.A); AddButton("flask", JoyButton.LeftShoulder); AddButton("pause", JoyButton.Start);
        AddButton("interact", JoyButton.DpadUp);
        AddButton("local_map",JoyButton.Back);AddButton("region_map",JoyButton.DpadRight);
        AddAxis("preserve", JoyAxis.TriggerLeft, 1); AddButton("track", JoyButton.DpadLeft);
        AddButton("ui_accept", JoyButton.A); AddButton("ui_cancel", JoyButton.B);
        AddButton("ui_up", JoyButton.DpadUp); AddButton("ui_down", JoyButton.DpadDown);
        AddButton("ui_left", JoyButton.DpadLeft); AddButton("ui_right", JoyButton.DpadRight);
        AddAxis("move_left", JoyAxis.LeftX, -1); AddAxis("move_right", JoyAxis.LeftX, 1);
        AddAxis("move_up", JoyAxis.LeftY, -1); AddAxis("move_down", JoyAxis.LeftY, 1);
        var config = new ConfigFile();
        if (config.Load(settingsPath) == Error.Ok)
            foreach (var name in Names)
                if (config.HasSectionKey("keys", name)) ReplaceKey(name, (Key)(long)config.GetValue("keys", name));
    }
    private static void AddButton(string action, JoyButton button) => InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = button, Device = -1 });
    private static void AddAxis(string action, JoyAxis axis, float value) => InputMap.ActionAddEvent(action, new InputEventJoypadMotion { Axis = axis, AxisValue = value, Device = -1 });
    public void Observe(InputEvent input)
    {
        if (input is InputEventMouse mouse) { PointerPosition = mouse.Position; pointerObserved = true; }
        if (input is InputEventJoypadButton || input is InputEventJoypadMotion motion && Mathf.Abs(motion.AxisValue) > 0.25f) Controller = true;
        if (input is InputEventKey or InputEventMouseButton) Controller = false;
        if (input.IsEcho()) return;
        if (input.IsActionPressed("evade")) pressed = SkillId.Traverse;
        else if (input.IsActionPressed("flask")) pressed = SkillId.Flask;
        else if (input.IsActionPressed("pulse")) pressed = SkillId.ShieldPulse;
        else if (input.IsActionPressed("lance")) pressed = SkillId.ChainLance;
        else if (input.IsActionPressed("special")) pressed = SkillId.Faultline;
    }
    public PlayerIntent Read(long tick, Vector2 player, Vector2 mouse)
    {
        var move = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        var devices = Input.GetConnectedJoypads();
        Vector2 stick = Vector2.Zero;
        if (devices.Count > 0) stick = new(Input.GetJoyAxis(devices[0], JoyAxis.RightX), Input.GetJoyAxis(devices[0], JoyAxis.RightY));
        if (stick.Length() > 0.22f) { lastAim = stick.Normalized(); Controller = true; }
        else if (pointerObserved && mouse.DistanceSquaredTo(lastMouse) > 1) { lastAim = (mouse - player).Normalized(); Controller = false; }
        else if (Controller && move.LengthSquared() > 0.01f) lastAim = move.Normalized();
        else if (pointerObserved && !Controller && mouse.DistanceSquaredTo(player) > 1) lastAim = (mouse - player).Normalized();
        lastMouse = mouse;
        // Higher-priority deliberate presses supersede a held basic attack.
        SkillId? requested = pressed ?? (Input.IsActionPressed("cleave") ? SkillId.Cleave : null);
        pressed = null;
        if (requested is { } id && (buffered is null || id != SkillId.Cleave)) { buffered = id; expires = tick + 6; }
        if (tick >= expires) buffered = null;
        return new(move, lastAim, buffered, PreservingMemories);
    }
    public void ClearBuffer() { buffered = null; pressed = null; }
    public void Disconnected()
    {
        Controller = false; ClearBuffer();
        // A removed device must not leave held movement or a basic attack active after resuming.
        foreach(var name in Names) Input.ActionRelease(name);
    }
    public string? Rebind(string action, Key key)
    {
        if (key is Key.Escape or Key.F3) return "Escape and F3 are reserved.";
        var conflict = Names.FirstOrDefault(n => n != action && InputMap.ActionGetEvents(n).OfType<InputEventKey>().Any(e => e.PhysicalKeycode == key));
        if (conflict is not null) return $"That key is used by {conflict.Replace('_', ' ')}. Choose another key.";
        ReplaceKey(action, key);
        var config = new ConfigFile();
        foreach (var name in Names)
        {
            var binding = InputMap.ActionGetEvents(name).OfType<InputEventKey>().FirstOrDefault();
            if (binding is not null) config.SetValue("keys", name, (long)binding.PhysicalKeycode);
        }
        var error = config.Save(settingsPath);
        if (error != Error.Ok) GD.PushWarning($"Could not save controls: {error}");
        return error == Error.Ok ? null : "Binding changed, but could not be saved.";
    }
    private static void ReplaceKey(string action, Key key)
    {
        foreach (var e in InputMap.ActionGetEvents(action).Where(e => e is InputEventKey or InputEventMouseButton).ToArray()) InputMap.ActionEraseEvent(action, e);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }
    public string Glyph(string action)
    {
        if (Controller)
            return action switch { "cleave" => "RB", "pulse" => "X", "lance" => "Y", "special" => "B", "evade" => "A", "flask" => "LB", "pause" => "START", "interact" => "D-PAD UP", "local_map"=>"VIEW", "region_map"=>"D-PAD RIGHT", "preserve"=>"LT", "track"=>"D-PAD LEFT", _ => "LS" };
        var key = InputMap.ActionGetEvents(action).OfType<InputEventKey>().FirstOrDefault();
        return key is null ? action=="move_to"?"RMB":"LMB" : OS.GetKeycodeString(key.PhysicalKeycode).ToUpperInvariant();
    }
}
