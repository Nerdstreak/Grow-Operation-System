namespace GrowDiary.Web.Infrastructure;

public sealed class AppPaths
{
    public AppPaths(string contentRootPath)
    {
        ContentRootPath = contentRootPath;

        var configuredPath = Environment.GetEnvironmentVariable("GROWDIARY_DB_PATH");

        DatabasePath = !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : Path.Combine(contentRootPath, "App_Data", "grow-diary.db");

        UploadRootPath = Path.Combine(contentRootPath, "wwwroot", "uploads");

        LegacyProjectDatabasePath = Path.Combine(contentRootPath, "App_Data", "legacy-grow-diary.db");
        LegacyWpfDatabasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Grow_Diary",
            "grow.db");
    }

    public string ContentRootPath { get; }
    public string DatabasePath { get; }
    public string UploadRootPath { get; }
    public string LegacyProjectDatabasePath { get; }
    public string LegacyWpfDatabasePath { get; }
}
