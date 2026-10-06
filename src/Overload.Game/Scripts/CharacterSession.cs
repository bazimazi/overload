using Godot;
using Overload.Content;
using Overload.Domain;

namespace Overload.Game;

public partial class Arena
{
    public CharacterStore? Character { get; private set; }
    public string SaveProblem { get; private set; } = "";
    private string characterPath = "";
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
            var path = IsSmoke ? $"user://tests/session-{Guid.NewGuid():N}" : "user://characters/" + name;
            characterPath = ProjectSettings.GlobalizePath(path);
            Character = new CharacterStore(characterPath, PracticeTier is { } tier ? () => EndlessFixtures.Reference(tier) : trainingBuild is { } build ? () => FrameRules.BuildFixture(frame,build) : standard ? () => FrameRules.Create(frame) : separate ? () => FrameRules.Create(frame,false) : IsSmoke ? null : () => FrameRules.Create(frame)); SaveProblem = "";
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
}
