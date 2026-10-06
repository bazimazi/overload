using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Overload.Domain;

/// <summary>Nonnegative, unbounded counter. JSON is always an exact canonical decimal string.</summary>
[JsonConverter(typeof(QuantityJsonConverter))]
public readonly record struct Quantity
{
    public BigInteger Value { get; }
    public Quantity(BigInteger value)
    {
        if (value.Sign < 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }
    public static Quantity Parse(string text)
    {
        if (string.IsNullOrEmpty(text) || (text.Length > 1 && text[0] == '0') || text.Any(c => c < '0' || c > '9'))
            throw new FormatException("Expected a canonical nonnegative decimal string.");
        return new(BigInteger.Parse(text, CultureInfo.InvariantCulture));
    }
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public sealed class QuantityJsonConverter : JsonConverter<Quantity>
{
    public override Quantity Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("Unbounded values must be strings.");
        try { return Quantity.Parse(reader.GetString()!); }
        catch (FormatException e) { throw new JsonException(e.Message, e); }
    }
    public override void Write(Utf8JsonWriter writer, Quantity value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}

/// <summary>Growing combat amounts use 1,000 subunits per displayed point.</summary>
public static class CombatMath
{
    public static BigInteger Points(BigInteger points) => points * 1000;
    public static int BarBasisPoints(BigInteger current, BigInteger maximum) => maximum <= 0 ? 0 : (int)(BigInteger.Clamp(current, 0, maximum) * 10000 / maximum);
    public static BigInteger Mitigate(BigInteger damage, BigInteger defense, BigInteger k, int capPercent)
    {
        if (damage < 0 || defense < 0 || k <= 0 || capPercent is < 0 or > 100) throw new ArgumentOutOfRangeException();
        // Choose the capped rational before rounding once at the final subunit boundary.
        return defense * 100 >= capPercent * (defense + k)
            ? damage * (100 - capPercent) / 100
            : damage * k / (defense + k);
    }
}
