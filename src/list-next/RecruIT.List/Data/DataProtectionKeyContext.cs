using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace RecruIT.List.Data;

public sealed class DataProtectionKeyContext(DbContextOptions<DataProtectionKeyContext> options)
    : DbContext(options),
        IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
}
