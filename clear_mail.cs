using System.Text.RegularExpressions;

public static class MailTextCleaner
{
    private static readonly Regex[] QuoteRegexes =
    {
        // Gmail (русский)
        new(@"^\s*\S+,\s+\d{1,2}.*?в\s+\d{1,2}:\d{2}.*?:\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase),

        // Gmail (английский)
        new(@"^\s*On .+?wrote:\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase),

        // Outlook
        new(@"^\s*-{5}Original Message-{5}\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase),

        // Русский Outlook
        new(@"^\s*-{5}Исходное сообщение-{5}\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase),

        // Строка "От:"
        new(@"^\s*>?\s*От:.*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase),

        // Строка "From:"
        new(@"^\s*>?\s*From:.*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase),
    };

    public static string RemoveQuotedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        foreach (var regex in QuoteRegexes)
        {
            var match = regex.Match(text);
            if (match.Success)
                return text[..match.Index].TrimEnd();
        }

        return text.TrimEnd();
    }
}