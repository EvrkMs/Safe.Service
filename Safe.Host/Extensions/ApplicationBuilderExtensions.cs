using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Safe.EntityFramework.Contexts;

namespace Safe.Host.Extensions;

internal static class ApplicationBuilderExtensions
{
    public static async Task ApplyDatabaseMigrationsAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SafeDbContext>();
        await db.Database.MigrateAsync();
    }
}
