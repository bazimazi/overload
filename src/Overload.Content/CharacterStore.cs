using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Overload.Domain;

namespace Overload.Content;

public enum SaveStage { TemporaryWritten, Flushed, Verified, BeforeReplace, Replaced }
public sealed class FutureSaveException() : IOException("This character requires a newer game version. Existing files were preserved.");
public sealed class BigIntegerStringConverter : JsonConverter<BigInteger>
{
    public override BigInteger Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("Large counters must be decimal strings");
        try { return Quantity.Parse(reader.GetString()!).Value; } catch (FormatException e) { throw new JsonException(e.Message, e); }
    }
    public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options) => writer.WriteStringValue(new Quantity(value).ToString());
}

/// <summary>Single-writer, revision-checked aggregate transactions with same-directory atomic replacement.</summary>
public sealed class CharacterStore
{
    // Operational input guard, not a cap on level, tier or legitimate counter values.
    public const int MaximumSaveBytes = 4 * 1024 * 1024;
    private sealed record Envelope(string Payload, string Sha256);
    public static JsonSerializerOptions JsonOptions { get; } = new() { WriteIndented = true, Converters = { new BigIntegerStringConverter(), new JsonStringEnumConverter(allowIntegerValues: false) } };
    private readonly string directory;
    private string CurrentPath => Path.Combine(directory, "character.json");
    public CharacterState State { get; private set; }
    public string Notice { get; private set; } = "";
    public Action<SaveStage>? Fault { get; set; }
    public CharacterStore(string directory, Func<CharacterState>? create = null)
    {
        this.directory = Path.GetFullPath(directory); Directory.CreateDirectory(this.directory);
        using var lease = new FileStream(Path.Combine(this.directory, "character.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var loaded = Load();
        State = loaded ?? (create ?? (() => new CharacterState()))();
        if (loaded is null) Write(State);
    }
    private CharacterState? Load()
    {
        var paths = new[] { CurrentPath, CurrentPath + ".tmp", CurrentPath + ".bak1", CurrentPath + ".bak2", CurrentPath + ".bak3" };
        var found = new List<(CharacterState State, string Path)>(); var any = false;
        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue; any = true;
            try { found.Add((Decode(ReadBounded(path)), path)); }
            catch (FutureSaveException) { throw; }
            catch (Exception e) when (e is JsonException or InvalidDataException or FormatException or ArgumentException) { }
        }
        if (found.Count == 0)
        {
            if (any) throw new InvalidDataException("No valid character snapshot. Files preserved; restore a backup or choose a new profile.");
            return null;
        }
        var winner = found.OrderByDescending(x => x.State.Revision).First();
        if (winner.Path != CurrentPath)
        {
            Notice = $"Recovered complete revision {winner.State.Revision} from {Path.GetFileName(winner.Path)}. Existing files retained.";
            // Promote recovery before exposing it, so a subsequent failed transaction cannot erase the only newest snapshot.
            var recovery = CurrentPath + ".recover";
            File.Copy(winner.Path, recovery, true);
            using (var stream = new FileStream(recovery, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) stream.Flush(true);
            Decode(File.ReadAllText(recovery));
            if (File.Exists(CurrentPath)) File.Replace(recovery, CurrentPath, CurrentPath + ".recovered-old", true);
            else File.Move(recovery, CurrentPath);
        }
        return winner.State;
    }
    public static string Encode(CharacterState state)
    {
        var payload = JsonSerializer.Serialize(state, JsonOptions);
        return JsonSerializer.Serialize(new Envelope(payload, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))), JsonOptions);
    }
    public static CharacterState Decode(string text)
    {
        if (text.Length > MaximumSaveBytes || Encoding.UTF8.GetByteCount(text) > MaximumSaveBytes) throw new InvalidDataException("Character snapshot exceeds the operational 4 MiB limit");
        var envelope = JsonSerializer.Deserialize<Envelope>(text, JsonOptions) ?? throw new InvalidDataException("Empty save");
        if (envelope.Payload is null || envelope.Sha256 != Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(envelope.Payload)))) throw new InvalidDataException("Save checksum mismatch");
        using var document = JsonDocument.Parse(envelope.Payload);
        if (!document.RootElement.TryGetProperty("SchemaVersion", out var schema) || !schema.TryGetInt32(out var version)) throw new InvalidDataException("Missing save schema");
        if (version > 6) throw new FutureSaveException();
        if (version < 1) throw new InvalidDataException("Unknown save version");
        if (document.RootElement.TryGetProperty("ContentVersion", out var content))
        {
            if(content.ValueKind!=JsonValueKind.String)throw new InvalidDataException("Invalid character content version");
            if(content.GetString()!="slice.v1")throw new FutureSaveException();
        }
        foreach (var key in new[] { "CharacterId", "Revision", "TotalXp", "Gold", "Alloy" })
            if (!document.RootElement.TryGetProperty(key, out _)) throw new InvalidDataException($"Missing character field: {key}");
        var state = JsonSerializer.Deserialize<CharacterState>(envelope.Payload, JsonOptions) ?? throw new InvalidDataException("Empty character");
        if (version >= 3 && state.Fracture is { ContentVersion: not (EndlessRules.Version or EndgameRules.Version or RegionalContent.ExpeditionVersion or FractureMapRules.Version) }) throw new FutureSaveException();
        if(state.Fracture?.Layout is { Version:not (ExpeditionGenerator.Version or RegionalContent.GeneratorVersion) })throw new FutureSaveException();
        if(state.Fracture?.Map is { GeneratorVersion:not FractureMapGenerator.Version })throw new FutureSaveException();
        if (version == 1) state = state with { SchemaVersion = 2, ValidatedLevel = Progression.LevelAt(state.TotalXp),
            Bindings = state.Bindings.IsDefault ? [] : [.. state.Bindings.Where(b => b is not null && CharacterRules.KnownPatterns.Contains(b.PatternId))],
            Journal = [.. state.Journal.IsDefault ? [] : state.Journal, "Migrated v1 character; removed bindings disabled without changing progression."] };
        if (version <= 2) state = state with { SchemaVersion = 3, FractureUnlocked = state.CheckpointRoom == 8 || state.HighestClearedTier > 0,
            HighestUnlockedTier = state.HighestClearedTier + 1, Chapter = EndlessRules.ChapterAt(state.HighestClearedTier + 1),
            ChapterOffers = [], PendingChapter = 0, SelectedRoute = "route.ash", Fracture = null, ChainLeg = 0,
            Journal = [.. state.Journal.TakeLast(31), "Migrated v2 character; Fracture board and exact attunement ledger enabled."] };
        if (version <= 3) state = state with { SchemaVersion = 4 };
        if (version <= 4) state = state with { SchemaVersion = 5, Frame = FrameId.Warden, RegionalCampaign = false,
            RedCovenantOwned = false, RedCovenantSelected = false, CovenantMastery = false };
        if(version<=5) state=state with { SchemaVersion=6, World=null };
        if(state.World is { Version:not WorldContent.Version })throw new FutureSaveException();
        CharacterRules.Validate(state); return state;
    }
    public bool Transact(long expectedRevision, string receipt, Func<CharacterState, CharacterState> update)
    {
        using var lease = new FileStream(Path.Combine(directory, "character.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (State.RecentTransactions.Contains(receipt)) return false;
        if (expectedRevision != State.Revision) throw new InvalidOperationException("Stale character transaction");
        // A crashed writer may have left a newer complete snapshot; never overwrite it with an old command.
        var disk = Load();
        if (disk is not null && disk.Revision != State.Revision) { State = disk; throw new InvalidOperationException("Character recovered; retry with its current revision"); }
        var candidate = update(State) with { Revision = checked(State.Revision + 1), RecentTransactions = [.. State.RecentTransactions.TakeLast(63), receipt] };
        if (candidate.CharacterId != State.CharacterId) throw new InvalidOperationException("Transaction cannot replace character identity");
        CharacterRules.Validate(candidate); Write(candidate); State = candidate; return true;
    }
    private void Write(CharacterState state)
    {
        CharacterRules.Validate(state);
        var temporary = CurrentPath + ".tmp";
        var bytes = Encoding.UTF8.GetBytes(Encode(state));
        if (bytes.Length > MaximumSaveBytes) throw new InvalidDataException("Character snapshot exceeds the operational 4 MiB limit");
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes); Fault?.Invoke(SaveStage.TemporaryWritten); stream.Flush(true); Fault?.Invoke(SaveStage.Flushed);
        }
        Decode(File.ReadAllText(temporary)); Fault?.Invoke(SaveStage.Verified);
        for (var i = 3; i >= 2; i--) if (File.Exists(CurrentPath + $".bak{i - 1}")) File.Copy(CurrentPath + $".bak{i - 1}", CurrentPath + $".bak{i}", true);
        Fault?.Invoke(SaveStage.BeforeReplace);
        if (File.Exists(CurrentPath)) File.Replace(temporary, CurrentPath, CurrentPath + ".bak1", true);
        else File.Move(temporary, CurrentPath);
        Fault?.Invoke(SaveStage.Replaced);
    }
    private static string ReadBounded(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumSaveBytes) throw new InvalidDataException("Character snapshot exceeds the operational 4 MiB limit");
        using var reader = new StreamReader(stream, Encoding.UTF8, true); return reader.ReadToEnd();
    }
}
