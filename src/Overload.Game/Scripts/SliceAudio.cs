using Godot;

namespace Overload.Game;
public partial class SliceAudio : Node
{
    public static readonly string[] Channels = ["Master", "Music", "Ambience", "Player", "Enemy", "UI"];
    private readonly Dictionary<string, AudioStreamWav> sounds = [];
    private readonly Dictionary<string, AudioStreamPlayer> loops = [];
    private readonly Dictionary<string, List<AudioStreamPlayer>> voices = [];
    private readonly Dictionary<string, int> volumes = [];
    private string settingsPath = "";
    private bool paused;
    private bool silentFixture;
    private float intensity, targetIntensity;
    private ulong lastWarning;
    public bool ReducedFlash { get; private set; }
    public bool ShowTutorialHints { get; private set; } = true;
    public int TextPercent { get; private set; } = 100;
    public bool HighContrast { get; private set; }
    public void Initialize(bool smoke)
    {
        silentFixture=smoke;
        settingsPath = smoke ? "user://tests/presentation.cfg" : "user://presentation.cfg";
        var config = new ConfigFile(); config.Load(settingsPath);
        foreach (var channel in Channels)
        {
            if (AudioServer.GetBusIndex(channel) < 0) { AudioServer.AddBus(); AudioServer.SetBusName(AudioServer.BusCount - 1, channel); AudioServer.SetBusSend(AudioServer.BusCount - 1, "Master"); }
            SetVolume(channel, Math.Clamp(config.GetValue("audio", channel, channel == "Master" ? 70 : channel is "Music" or "Ambience" ? 50 : 80).AsInt32(), 0, 100), false);
            voices[channel] = [];
        }
        foreach (var name in new[] { "strike", "hit", "memory", "ui", "bell", "music", "ambience", "combat", "cast", "evade", "defeat", "heal", "overload", "reward", "warning" })
            sounds[name] = GD.Load<AudioStreamWav>($"res://Assets/{(name is "memory" or "ui" or "bell" ? "" : "Audio/")}{name}.wav");
        ReducedFlash = config.GetValue("accessibility", "reduced_flash", false).AsBool();
        ShowTutorialHints = config.GetValue("accessibility", "tutorial_hints", true).AsBool();
        TextPercent = config.GetValue("accessibility", "text_percent", 100).AsInt32() == 125 ? 125 : 100;
        HighContrast = config.GetValue("accessibility", "high_contrast", false).AsBool();
        foreach (var name in new[] { "music", "ambience", "combat" })
        {
            var stream = sounds[name]; stream.LoopMode = AudioStreamWav.LoopModeEnum.Forward; stream.LoopEnd = (int)Math.Round(stream.GetLength() * stream.MixRate);
            var player = new AudioStreamPlayer { Stream = stream, Bus = name == "ambience" ? "Ambience" : "Music", VolumeDb = name == "combat" ? -60 : name == "music" ? -4 : 0 };
            AddChild(player); loops[name] = player; if (!smoke) player.Play();
        }
    }
    public void StartLoops() { foreach (var player in loops.Values) if (!player.Playing) player.Play(); }
    public void SetPaused(bool value) => paused = value;
    public void SetRegion(Overload.Domain.Region region,bool restored)
    {
        if(loops.TryGetValue("ambience",out var ambience))
        {
            var path=$"res://Assets/Audio/world-{region.ToString().ToLowerInvariant()}.wav";
            if(ResourceLoader.Exists(path)){var stream=GD.Load<AudioStreamWav>(path);stream.LoopMode=AudioStreamWav.LoopModeEnum.Forward;stream.LoopEnd=(int)(stream.GetLength()*stream.MixRate);ambience.Stream=stream;if(!silentFixture&&!ambience.Playing)ambience.Play();}
            ambience.PitchScale=restored?.88f:1;
        }
    }
    public void SetHearth()
    {if(loops.TryGetValue("ambience",out var ambience)){ambience.Stream=sounds["ambience"];ambience.PitchScale=1;if(!silentFixture&&!ambience.Playing)ambience.Play();}}
    public void SetEncounter(bool combat, bool boss) => targetIntensity = combat ? boss ? 1 : .55f : 0;
    public override void _Process(double delta)
    {
        intensity = Mathf.Lerp(intensity,targetIntensity,1-MathF.Exp(-(float)delta*1.8f));
        if (loops.TryGetValue("combat",out var layer)) layer.VolumeDb = Mathf.LinearToDb(Math.Max(.001f,intensity)) - (paused?10:0);
        if (loops.TryGetValue("music",out var music)) music.VolumeDb = -4 + intensity * 3 - (paused?7:0);
        if (loops.TryGetValue("ambience",out var ambience)) ambience.VolumeDb = -intensity * 3 - (paused?7:0);
    }
    public void Warn()
    {
        var now=Time.GetTicksMsec();
        if (now-lastWarning < 180) return;
        lastWarning=now;Play("warning","Enemy");
    }
    public int Volume(string channel) => volumes[channel];
    public void SetVolume(string channel, int value, bool save = true)
    {
        volumes[channel] = value; var bus = AudioServer.GetBusIndex(channel);
        AudioServer.SetBusMute(bus, value == 0); AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(Math.Max(.001f, value / 100f)));
        if (save) Save();
    }
    public void ToggleFlash() { ReducedFlash = !ReducedFlash; Save(); }
    public void ToggleHints() { ShowTutorialHints = !ShowTutorialHints; Save(); }
    public void ToggleText() { TextPercent = TextPercent == 100 ? 125 : 100; Save(); }
    public void ToggleContrast() { HighContrast = !HighContrast; Save(); }
    private void Save()
    {
        var config = new ConfigFile();
        foreach (var pair in volumes) config.SetValue("audio", pair.Key, pair.Value);
        config.SetValue("accessibility", "reduced_flash", ReducedFlash); config.SetValue("accessibility", "tutorial_hints", ShowTutorialHints);
        config.SetValue("accessibility", "text_percent", TextPercent); config.SetValue("accessibility", "high_contrast", HighContrast);
        if (config.Save(settingsPath) != Error.Ok) GD.PushWarning("Presentation settings could not be saved");
    }
    public void Play(string name, string channel)
    {
        if (!sounds.TryGetValue(name, out var stream)) return;
        var pool = voices[channel]; var player = pool.FirstOrDefault(p => !p.Playing);
        if (player is null)
        {
            if (pool.Count == 4) return;
            player = new AudioStreamPlayer { Bus = channel }; pool.Add(player); AddChild(player);
        }
        player.PitchScale = name is "strike" or "hit" ? .94f + (voiceSequence++ % 5) * .03f : 1;
        player.Stream = stream; player.Play();
    }
    private int voiceSequence;
    public void StopAll()
    {
        foreach (var player in GetChildren().OfType<AudioStreamPlayer>()) { player.Stop(); player.Stream = null; }
        voices.Clear(); loops.Clear(); sounds.Clear();
    }
    public override void _ExitTree() => StopAll();
}
