using Godot;
using Overload.Content;
using Overload.Domain;

namespace Overload.Game;

public partial class Arena
{
    public CharacterStore? Character { get; private set; }
    public string SaveProblem { get; private set; } = "";
    private string characterPath = "";
    private readonly string testProfiles = "user://tests/profiles-" + Guid.NewGuid().ToString("N");
    private string ProfileRoot => IsSmoke ? testProfiles : "user://characters";
    public void ReloadCharacterForCheck()
    {
        if(!IsSmoke || Playing) throw new InvalidOperationException("Reload fixture requires an isolated idle character");
        Character = new CharacterStore(characterPath); ApplyCharacterBuild();
    }
    public void OpenCharacter(bool separate = false,bool standard = false, FrameId frame = FrameId.Warden, int? trainingBuild = null)
    {
        try
        {
            var config = new ConfigFile(); if (!IsSmoke) config.Load("user://profile.cfg");
            var name = separate ? frame.ToString().ToLowerInvariant()+"-" + Guid.NewGuid().ToString("N") : config.GetValue("profile", "name", "warden").AsString();
            if (name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) throw new InvalidDataException("Invalid profile folder name");
            var path = ProfileRoot + "/" + name;
            characterPath = ProjectSettings.GlobalizePath(path);
            Character = new CharacterStore(characterPath, PracticeTier is { } tier ? () => EndlessFixtures.Reference(tier) : trainingBuild is { } build ? () => FrameRules.BuildFixture(frame,build) : standard ? () => IsSmoke?FrameRules.Create(frame):WorldRules.Enroll(FrameRules.Create(frame)) : separate ? () => FrameRules.Create(frame,false) : IsSmoke ? null : () => WorldRules.Enroll(FrameRules.Create(frame))); SaveProblem = "";
            ApplyCharacterBuild();
            if (separate && !IsSmoke) { config.SetValue("profile", "name", name); if (config.Save("user://profile.cfg") != Error.Ok) throw new IOException("Could not save profile selection"); }
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        { Character = null; SaveProblem = e.Message; }
    }
    public bool UpdateCharacter(Func<CharacterState, CharacterState> change)
    {
        if (Playing || OathPractice || Character is null) return false;
        try
        {
            Character.Transact(Character.State.Revision, Guid.NewGuid().ToString("N"), change);
            ApplyCharacterBuild();
            SaveProblem = ""; return true;
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException)
        { SaveProblem = e.Message; return false; }
    }
    private void ApplyCharacterBuild(bool force = false)
    {
        if (Character is null) return;
        if (force || !OS.GetCmdlineUserArgs().Contains("--smoke-test")) { Balance = FoundationRules.Build(BaseBalance, Character.State); PlayerState = new(Balance, new ArenaActionPreflight(this), Character.State, EncounterTier); }
        PlayerState.TryChangeBindings(Character.State.Bindings);
        PlayerState.TryChangeOaths(Character.State.ElsewhereSelected ? ["oath.elsewhere"] : Character.State.RedCovenantSelected ? ["oath.red-covenant"] : []);
    }
    public IEnumerable<(string Slot, CharacterState? State, string Notice)> SavedCharacters()
    {
        var root = ProjectSettings.GlobalizePath(ProfileRoot);
        if (!Directory.Exists(root)) yield break;
        foreach (var folder in Directory.EnumerateDirectories(root).OrderBy(p=>p))
        {
            var slot = System.IO.Path.GetFileName(folder); var file = System.IO.Path.Combine(folder,"character.json");
            if (slot.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-') || !System.IO.File.Exists(file)) continue;
            CharacterState? state = null; string notice = "";
            try { if(new System.IO.FileInfo(file).Length>CharacterStore.MaximumSaveBytes)throw new InvalidDataException("Save exceeds operational size limit"); state=CharacterStore.Decode(System.IO.File.ReadAllText(file)); }
            catch(Exception e) when(e is IOException or System.Text.Json.JsonException or FormatException or ArgumentException) { notice=e.Message; }
            yield return (slot,state,notice);
        }
    }
    public bool SelectCharacter(string slot)
    {
        if(Playing || slot.Length==0 || slot.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))return false;
        try
        {
            var path=ProjectSettings.GlobalizePath(ProfileRoot+"/"+slot);
            if(!System.IO.File.Exists(System.IO.Path.Combine(path,"character.json")))throw new IOException("Character snapshot is missing");
            var selected=new CharacterStore(path);
            if(!IsSmoke)
            {
                var config=new ConfigFile();config.Load("user://profile.cfg");config.SetValue("profile","name",slot);
                if(config.Save("user://profile.cfg")!=Error.Ok)throw new IOException("Could not save character selection");
            }
            Character=selected;characterPath=path;ApplyCharacterBuild();SaveProblem="";return true;
        }
        catch(Exception e) when(e is IOException or System.Text.Json.JsonException or UnauthorizedAccessException) { SaveProblem=e.Message;return false; }
    }
}
