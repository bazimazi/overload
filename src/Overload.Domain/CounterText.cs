using System.Globalization;
using System.Numerics;

namespace Overload.Domain;
public static class CounterText
{
    public static string Short(BigInteger value)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        if (text.Length <= 9) return text;
        return $"{text[0]}.{text.Substring(1, 3)}e{(text.Length - 1).ToString(CultureInfo.InvariantCulture)}";
    }
}
