namespace GrowDiary.Web.Api.Contracts;

/// <summary>
/// Admin key state for remote (phone/LAN) access. The <see cref="Key"/> is only
/// ever returned to local (desktop) requests so it can be copied there.
/// </summary>
public sealed record AdminKeyDto(bool Configured, string? Key);
