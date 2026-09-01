namespace VideoTinyTool.Localization;

public static class PluralRules
{
    public const string Slavic = "slavic";
    public const string OneOther = "one-other";

    private static readonly string[] SlavicLanguages = ["ru", "uk", "be"];

    public static string RuleFor(string language) =>
        SlavicLanguages.Contains(language, StringComparer.OrdinalIgnoreCase) ? Slavic : OneOther;

    public static string Form(string rule, int count)
    {
        var value = Math.Abs(count);
        return string.Equals(rule, Slavic, StringComparison.OrdinalIgnoreCase)
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
