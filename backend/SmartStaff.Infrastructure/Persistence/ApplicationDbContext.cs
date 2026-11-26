using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartStaff.Domain.Staffing;

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

    public DbSet<HealthcareFacility> HealthcareFacilities => Set<HealthcareFacility>();

    public DbSet<FacilityLocation> FacilityLocations => Set<FacilityLocation>();

    public DbSet<FacilityMembership> FacilityMemberships => Set<FacilityMembership>();

    public DbSet<JobRequisition> JobRequisitions => Set<JobRequisition>();

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

        builder.Entity<HealthcareFacility>(entity =>
        {
            entity.Property(facility => facility.Name).HasMaxLength(200).IsRequired();
            entity.Property(facility => facility.Description).HasMaxLength(2000);
            entity.Property(facility => facility.Address).HasMaxLength(200).IsRequired();
            entity.Property(facility => facility.City).HasMaxLength(100).IsRequired();
            entity.Property(facility => facility.State).HasMaxLength(2).IsRequired();
            entity.Property(facility => facility.ZipCode).HasMaxLength(10).IsRequired();
            entity.Property(facility => facility.ContactEmail).HasMaxLength(256).IsRequired();
            entity.Property(facility => facility.ContactPhone).HasMaxLength(32).IsRequired();
            entity.Property(facility => facility.FacilityType).HasConversion<string>().HasMaxLength(40);
            entity.Property(facility => facility.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(facility => facility.Name);
            entity.HasIndex(facility => new { facility.State, facility.City });
        });

        builder.Entity<FacilityLocation>(entity =>
        {
            entity.Property(location => location.LocationName).HasMaxLength(200).IsRequired();
            entity.Property(location => location.Address).HasMaxLength(200).IsRequired();
            entity.Property(location => location.City).HasMaxLength(100).IsRequired();
            entity.Property(location => location.State).HasMaxLength(2).IsRequired();
            entity.Property(location => location.ZipCode).HasMaxLength(10).IsRequired();
            entity.HasIndex(location => location.FacilityId);
            entity.HasOne(location => location.Facility)
                .WithMany(facility => facility.Locations)
                .HasForeignKey(location => location.FacilityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FacilityMembership>(entity =>
        {
            entity.Property(membership => membership.UserId).HasMaxLength(450).IsRequired();
            entity.Property(membership => membership.MembershipRole).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(membership => new { membership.FacilityId, membership.UserId }).IsUnique();
            entity.HasOne(membership => membership.Facility)
                .WithMany(facility => facility.Memberships)
                .HasForeignKey(membership => membership.FacilityId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(membership => membership.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobRequisition>(entity =>
        {
            entity.Property(requisition => requisition.JobTitle).HasMaxLength(200).IsRequired();
            entity.Property(requisition => requisition.Description).HasMaxLength(4000).IsRequired();
            entity.Property(requisition => requisition.Specialty).HasMaxLength(120).IsRequired();
            entity.Property(requisition => requisition.RequiredCertifications).HasMaxLength(500).IsRequired();
            entity.Property(requisition => requisition.CreatedBy).HasMaxLength(450).IsRequired();
            entity.Property(requisition => requisition.AssignedRecruiterId).HasMaxLength(450);
            entity.Property(requisition => requisition.PayRateMin).HasPrecision(12, 2);
            entity.Property(requisition => requisition.PayRateMax).HasPrecision(12, 2);
            entity.Property(requisition => requisition.EmploymentType).HasConversion<string>().HasMaxLength(20);
            entity.Property(requisition => requisition.ShiftType).HasConversion<string>().HasMaxLength(20);
            entity.Property(requisition => requisition.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(requisition => requisition.FacilityId);
            entity.HasIndex(requisition => requisition.Status);
            entity.HasIndex(requisition => requisition.AssignedRecruiterId);
            entity.HasIndex(requisition => requisition.Specialty);
            entity.HasOne(requisition => requisition.Facility)
                .WithMany(facility => facility.Requisitions)
                .HasForeignKey(requisition => requisition.FacilityId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(requisition => requisition.Location)
                .WithMany()
                .HasForeignKey(requisition => requisition.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(requisition => requisition.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(requisition => requisition.AssignedRecruiterId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("CK_JobRequisitions_PayRange", "\"PayRateMax\" >= \"PayRateMin\""));
        });
    }
}
