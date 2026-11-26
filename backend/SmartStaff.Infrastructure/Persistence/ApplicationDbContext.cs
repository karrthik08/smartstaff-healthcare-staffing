using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SmartStaff.Infrastructure.Persistence;

public sealed class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = "";

    public string LastName { get; set; } = "";

    public bool IsActive { get; set; } = true;
}

public sealed class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(user => user.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(user => user.LastName).HasMaxLength(100).IsRequired();
            entity.Property(user => user.IsActive).HasDefaultValue(true);
        });
        builder.Entity<IdentityRole>().ToTable("Roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<string>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims");

        builder.Entity<AuditLog>(entity =>
        {
            entity.Property(log => log.Action).HasMaxLength(80).IsRequired();
            entity.Property(log => log.UserId).HasMaxLength(450);
            entity.Property(log => log.Detail).HasMaxLength(500);
            entity.Property(log => log.IpAddress).HasMaxLength(64);
            entity.HasIndex(log => log.CreatedAt);
        });

        builder.Entity<PasswordResetToken>(entity =>
        {
            entity.Property(token => token.UserId).HasMaxLength(450).IsRequired();
            entity.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(token => token.TokenHash).IsUnique();
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.Property(token => token.UserId).HasMaxLength(450).IsRequired();
            entity.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(token => token.FamilyId).HasMaxLength(64).IsRequired();
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasIndex(token => token.FamilyId);
        });
    }
}
