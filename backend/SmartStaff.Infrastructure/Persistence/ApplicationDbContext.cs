using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartStaff.Domain.Recruitment;
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

    public DbSet<ClinicianProfile> ClinicianProfiles => Set<ClinicianProfile>();

    public DbSet<ClinicianCredential> ClinicianCredentials => Set<ClinicianCredential>();

    public DbSet<JobApplication> JobApplications => Set<JobApplication>();

    public DbSet<ApplicationStatusHistory> ApplicationStatusHistory => Set<ApplicationStatusHistory>();

    public DbSet<Placement> Placements => Set<Placement>();

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

        builder.Entity<ClinicianProfile>(entity =>
        {
            entity.Property(profile => profile.UserId).HasMaxLength(450).IsRequired();
            entity.Property(profile => profile.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(profile => profile.LastName).HasMaxLength(100).IsRequired();
            entity.Property(profile => profile.PhoneNumber).HasMaxLength(32).IsRequired();
            entity.Property(profile => profile.ProfessionalTitle).HasMaxLength(120).IsRequired();
            entity.Property(profile => profile.Specialty).HasMaxLength(120).IsRequired();
            entity.Property(profile => profile.PreferredLocations).HasMaxLength(500).IsRequired();
            entity.Property(profile => profile.EmploymentPreference).HasConversion<string>().HasMaxLength(20);
            entity.Property(profile => profile.ProfileStatus).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(profile => profile.UserId).IsUnique();
            entity.HasIndex(profile => profile.Specialty);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(profile => profile.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClinicianCredential>(entity =>
        {
            entity.Property(credential => credential.CredentialName).HasMaxLength(160).IsRequired();
            entity.Property(credential => credential.IssuingOrganization).HasMaxLength(160).IsRequired();
            entity.Property(credential => credential.CredentialNumber).HasMaxLength(80).IsRequired();
            entity.Property(credential => credential.VerifiedBy).HasMaxLength(450);
            entity.Property(credential => credential.CredentialType).HasConversion<string>().HasMaxLength(20);
            entity.Property(credential => credential.VerificationStatus).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(credential => credential.ClinicianProfileId);
            entity.HasOne(credential => credential.Profile)
                .WithMany(profile => profile.Credentials)
                .HasForeignKey(credential => credential.ClinicianProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<JobApplication>(entity =>
        {
            entity.Property(application => application.RecruiterId).HasMaxLength(450);
            entity.Property(application => application.Notes).HasMaxLength(2000).IsRequired();
            entity.Property(application => application.Status).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(application => new { application.JobRequisitionId, application.ClinicianProfileId }).IsUnique();
            entity.HasIndex(application => application.Status);
            entity.HasIndex(application => application.ClinicianProfileId);
            entity.HasOne(application => application.Profile)
                .WithMany()
                .HasForeignKey(application => application.ClinicianProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JobRequisition>()
                .WithMany()
                .HasForeignKey(application => application.JobRequisitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationStatusHistory>(entity =>
        {
            entity.Property(history => history.ChangedBy).HasMaxLength(450).IsRequired();
            entity.Property(history => history.Reason).HasMaxLength(500).IsRequired();
            entity.Property(history => history.PreviousStatus).HasConversion<string>().HasMaxLength(30);
            entity.Property(history => history.NewStatus).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(history => history.JobApplicationId);
            entity.HasOne(history => history.Application)
                .WithMany(application => application.History)
                .HasForeignKey(history => history.JobApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Placement>(entity =>
        {
            entity.Property(placement => placement.AgreedPayRate).HasPrecision(12, 2);
            entity.Property(placement => placement.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(placement => placement.JobApplicationId).IsUnique();
            entity.HasIndex(placement => placement.FacilityId);
            entity.HasIndex(placement => placement.ClinicianProfileId);
            entity.HasOne(placement => placement.Application)
                .WithMany()
                .HasForeignKey(placement => placement.JobApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<HealthcareFacility>()
                .WithMany()
                .HasForeignKey(placement => placement.FacilityId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ClinicianProfile>()
                .WithMany()
                .HasForeignKey(placement => placement.ClinicianProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("CK_Placements_PayRate", "\"AgreedPayRate\" > 0"));
        });
    }
}
