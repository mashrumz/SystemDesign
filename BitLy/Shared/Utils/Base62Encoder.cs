namespace Shared.Utils;

public static class Base62Encoder
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string Encode(long number)
    {
        if (number == 0) return "0";

        var result = new System.Text.StringBuilder();
        while (number > 0)
        {
            result.Insert(0, Alphabet[(int)(number % 62)]);
            number /= 62;
        }
        return result.ToString();
    }

    public static long Decode(string encoded)
    {
        long result = 0;
        foreach (var c in encoded)
        {
            result = result * 62 + Alphabet.IndexOf(c);
        }
        return result;
    }
}