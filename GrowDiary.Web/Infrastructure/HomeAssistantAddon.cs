using GrowDiary.Web.Models;

namespace GrowDiary.Web.Infrastructure;

/// <summary>
/// Detects and describes the Home Assistant add-on runtime.
///
/// When Grow OS runs as a Home Assistant add-on, the Supervisor injects a
/// <c>SUPERVISOR_TOKEN</c> and proxies the Home Assistant core API at
/// <c>http://supervisor/core</c>. That means the HA connection needs no manual
/// setup at all — no URL, no long-lived access token. This helper turns that
/// environment into effective <see cref="HomeAssistantSettings"/> so every
/// runtime consumer (live dashboard, snapshot worker, camera proxy, entity
/// picker) works out of the box inside the add-on.
/// </summary>
public static class HomeAssistantAddon
{
    public const string SupervisorTokenEnvironmentVariable = "SUPERVISOR_TOKEN";

    /// <summary>Home Assistant core API, reachable from any add-on via the Supervisor proxy.</summary>
    public const string SupervisorCoreUrl = "http://supervisor/core";

    /// <summary>The Supervisor-provided access token, or null when not running as an add-on.</summary>
    public static string? SupervisorToken
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(SupervisorTokenEnvironmentVariable);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    /// <summary>True when Grow OS is running inside a Home Assistant add-on.</summary>
    public static bool IsRunningAsAddon => SupervisorToken is not null;

    /// <summary>
    /// Returns the effective Home Assistant connection. Inside the add-on the
    /// Supervisor-provided URL + token always win (zero-config); otherwise the
    /// user's stored settings apply unchanged.
    /// </summary>
    public static HomeAssistantSettings ResolveEffective(HomeAssistantSettings stored)
    {
        var token = SupervisorToken;
        if (token is null)
        {
            return stored;
        }

        return new HomeAssistantSettings
        {
            BaseUrl = SupervisorCoreUrl,
            AccessToken = token,
            Enabled = true,
        };
    }
}
