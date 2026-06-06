using System.Globalization;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed partial class HardwareRepository : RepositoryBase
{
    public HardwareRepository(AppPaths paths) : base(paths)
    {
    }

}
