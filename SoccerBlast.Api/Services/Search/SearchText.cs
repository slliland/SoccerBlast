using System.Globalization;
using System.Text;

namespace SoccerBlast.Api.Services.Search;

public static class SearchText
{
    public static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";

        s = s.Trim().ToLowerInvariant();

        // Remove diacritics
        var formD = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        s = sb.ToString().Normalize(NormalizationForm.FormC);

        // Keep letters/digits/spaces only
        sb.Clear();
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                sb.Append(ch);
            else
                sb.Append(' ');
        }

        // collapse spaces
        return string.Join(' ', sb.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
