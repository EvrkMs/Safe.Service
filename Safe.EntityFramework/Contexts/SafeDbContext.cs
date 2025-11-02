using Microsoft.EntityFrameworkCore;
using Safe.Domain.Entities;

namespace Safe.EntityFramework.Contexts;

public class SafeDbContext(DbContextOptions opt) : DbContext(opt)
{
    public DbSet<SafeChange> SafeChanges => Set<SafeChange>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SafeDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
