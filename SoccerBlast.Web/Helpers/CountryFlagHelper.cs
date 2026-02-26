namespace SoccerBlast.Web.Helpers;

/// <summary>Returns flag emoji and ISO2 codes for country names. Used by Team and Player pages.</summary>
public static class CountryFlagHelper
{
    /// <summary>Returns flag emoji for a country name (e.g. Italy -> 🇮🇹). Uses Unicode regional indicators. Empty if unknown.</summary>
    public static string CountryFlag(string? countryName)
    {
        if (string.IsNullOrWhiteSpace(countryName)) return "";
        var code = CountryToIso2(countryName.Trim());
        if (code == null || code.Length != 2) return "";
        const int regionalIndicatorA = 0x1F1E6;
        var a = char.ConvertFromUtf32(regionalIndicatorA + char.ToUpperInvariant(code[0]) - 'A');
        var b = char.ConvertFromUtf32(regionalIndicatorA + char.ToUpperInvariant(code[1]) - 'A');
        return a + b;
    }

    public static string? CountryToIso2(string name)
    {
        if (CountryIso2.TryGetValue(name, out var code)) return code;
        if (name.Contains(',')) return CountryIso2.GetValueOrDefault(name.Split(',')[0].Trim());
        return null;
    }

    private static readonly Dictionary<string, string> CountryIso2 = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Argentina"] = "AR", ["Australia"] = "AU", ["Belgium"] = "BE", ["Brazil"] = "BR", ["Cameroon"] = "CM",
        ["Canada"] = "CA", ["Colombia"] = "CO", ["Croatia"] = "HR", ["Denmark"] = "DK", ["Ecuador"] = "EC",
        ["England"] = "GB", ["United Kingdom"] = "GB", ["UK"] = "GB", ["France"] = "FR", ["Germany"] = "DE", ["Ghana"] = "GH", ["Italy"] = "IT",
        ["Japan"] = "JP", ["Mexico"] = "MX", ["Morocco"] = "MA", ["Netherlands"] = "NL", ["Nigeria"] = "NG",
        ["Poland"] = "PL", ["Portugal"] = "PT", ["Scotland"] = "GB", ["Senegal"] = "SN", ["South Korea"] = "KR",
        ["Spain"] = "ES", ["Switzerland"] = "CH", ["Turkey"] = "TR", ["Ukraine"] = "UA", ["United States"] = "US",
        ["USA"] = "US", ["Uruguay"] = "UY", ["Wales"] = "GB", ["Côte d'Ivoire"] = "CI",
        ["Ivory Coast"] = "CI", ["Czech Republic"] = "CZ", ["Czechia"] = "CZ", ["Russia"] = "RU",
        ["Egypt"] = "EG", ["Algeria"] = "DZ", ["Tunisia"] = "TN", ["Serbia"] = "RS", ["Bosnia and Herzegovina"] = "BA",
        ["Slovakia"] = "SK", ["Austria"] = "AT", ["Sweden"] = "SE", ["Norway"] = "NO", ["Ireland"] = "IE",
        ["Northern Ireland"] = "GB", ["Greece"] = "GR", ["Romania"] = "RO", ["Bulgaria"] = "BG", ["Hungary"] = "HU",
        ["Finland"] = "FI", ["Iceland"] = "IS", ["New Zealand"] = "NZ", ["South Africa"] = "ZA",
        ["China"] = "CN", ["India"] = "IN", ["Saudi Arabia"] = "SA", ["Iran"] = "IR", ["Qatar"] = "QA",
        ["United Arab Emirates"] = "AE", ["UAE"] = "AE", ["Israel"] = "IL", ["Malta"] = "MT", ["Cyprus"] = "CY",
        ["Luxembourg"] = "LU", ["Slovenia"] = "SI", ["North Macedonia"] = "MK", ["Kosovo"] = "XK",
        ["Venezuela"] = "VE", ["Chile"] = "CL", ["Peru"] = "PE", ["Paraguay"] = "PY", ["Bolivia"] = "BO",
        ["Costa Rica"] = "CR", ["Panama"] = "PA", ["Jamaica"] = "JM", ["Trinidad and Tobago"] = "TT",
        ["Honduras"] = "HN", ["El Salvador"] = "SV", ["Guatemala"] = "GT", ["Nicaragua"] = "NI",
        ["Dominican Republic"] = "DO", ["Curacao"] = "CW", ["Suriname"] = "SR", ["Guyana"] = "GY",
    };
}
