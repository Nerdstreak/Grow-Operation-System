namespace GrowDiary.Web.Models;

public sealed class DashboardStats
{
    public int TotalGrows { get; set; }
    public int ActiveGrows { get; set; }
    public int ArchivedGrows { get; set; }
    public int AbortedGrows { get; set; }
    public int Measurements { get; set; }
    public int Photos { get; set; }
}
