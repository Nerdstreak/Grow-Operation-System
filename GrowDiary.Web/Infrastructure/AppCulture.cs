using System.Globalization;

namespace GrowDiary.Web.Infrastructure;

/// <summary>
/// The culture used to format numbers and dates for the user.
///
/// Resolved once, with a fallback, because <c>GetCultureInfo("de-DE")</c> throws outright
/// when the runtime is built without ICU or started in globalization-invariant mode. In a
/// static field that turns into a TypeInitializationException — the whole feature dies at
/// first touch rather than merely printing a dot instead of a comma.
/// </summary>
public static class AppCulture
{
    public static readonly CultureInfo German = Resolve("de-DE");

    private static CultureInfo Resolve(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
