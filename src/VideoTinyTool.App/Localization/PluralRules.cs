namespace VideoTinyTool.Localization;

public static class PluralRules
{
    private static readonly string[] SlavicLanguages = ["ru", "uk", "be"];

    public static string Form(string language, int count)
    {
        var value = Math.Abs(count);
        return SlavicLanguages.Contains(language, StringComparer.OrdinalIgnoreCase)
            ? SlavicForm(value)
            : value == 1 ? "one" : "other";
    }

    private static string SlavicForm(int count)
    {
        var tens = count % 100;
        if (tens is >= 11 and <= 14)
        {
            return "many";
        }

        return (count % 10) switch
        {
            1 => "one",
            2 or 3 or 4 => "few",
            _ => "many"
        };
    }
}
