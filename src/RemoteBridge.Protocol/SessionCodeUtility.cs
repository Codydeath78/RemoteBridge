namespace RemoteBridge.Protocol;

public static class SessionCodeUtility
{
    public static string Normalize(
        string? value)
    {
        if (
            string.IsNullOrWhiteSpace(
                value
            )
        )
        {
            return string.Empty;
        }

        return new string(
            value
                .Where(char.IsDigit)
                .ToArray()
        );
    }


    public static bool IsValid(
        string? value)
    {
        return Normalize(
            value
        ).Length == 9;
    }


    public static string Format(
        string? value)
    {
        string digits =
            Normalize(
                value
            );

        if (
            digits.Length != 9
        )
        {
            return value?.Trim()
                ?? string.Empty;
        }

        return
            $"{digits[..3]} " +
            $"{digits[3..6]} " +
            $"{digits[6..9]}";
    }
}