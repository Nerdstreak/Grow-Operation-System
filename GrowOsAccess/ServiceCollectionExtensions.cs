using Microsoft.Extensions.DependencyInjection;

namespace GrowOsAccess;

/// <summary>Die Anbindung an Grow OS in einem Rutsch registrieren.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Supervisor-Draht, Suche und Leser eintragen.</summary>
    /// <param name="adresse">
    /// Eine von Hand eingetragene Grow-OS-Adresse; leer lassen, wenn gesucht werden soll.
    /// </param>
    public static IServiceCollection AddGrowOsAccess(this IServiceCollection services, string? adresse)
    {
        services.AddSingleton(new GrowOsOptions { Adresse = adresse ?? string.Empty });
        services.AddHttpClient(SupervisorClient.HttpClientName, klient =>
        {
            klient.BaseAddress = new Uri("http://supervisor/");
            klient.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddSingleton<SupervisorClient>();

        // Die Suche merkt sich die gefundene Adresse, muss also ein Singleton sein
        // — ein typisierter Klient waere transient und der Merker damit wertlos.
        services.AddHttpClient(GrowOsDiscovery.HttpClientName,
            klient => klient.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<GrowOsDiscovery>();

        services.AddHttpClient<GrowOsReader>(klient => klient.Timeout = TimeSpan.FromSeconds(30));
        return services;
    }
}
