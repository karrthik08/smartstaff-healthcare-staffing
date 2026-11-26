using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartStaff.Application.Authentication;
using SmartStaff.Application.Common;
using SmartStaff.Application.Recruitment;
using SmartStaff.Application.Staffing;
using SmartStaff.Domain.Recruitment;
using SmartStaff.Domain.Staffing;
using SmartStaff.Infrastructure.Identity;
using SmartStaff.Infrastructure.Persistence;
using SmartStaff.Infrastructure.Staffing;

namespace SmartStaff.Infrastructure.Recruitment;

public sealed class ClinicianService : IClinicianService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditWriter _audit;

    public ClinicianService(ApplicationDbContext db, UserManager<ApplicationUser> users, IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    public async Task<OperationResult<ClinicianProfileDto>> GetMineAsync(string userId, CancellationToken cancellationToken)
    {
        if (!await IsClinicianAsync(userId))
        {
            return OperationResult<ClinicianProfileDto>.Fail(403, "Forbidden", "Only a clinician can open this profile.");
        }

        var profile = await _db.ClinicianProfiles.AsNoTracking().Include(item => item.Credentials)
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return profile is null
            ? OperationResult<ClinicianProfileDto>.Fail(404, "Not found", "Create your professional profile to continue.")
            : OperationResult<ClinicianProfileDto>.Ok(Map(profile, true));
    }

    public async Task<OperationResult<ClinicianProfileDto>> SaveMineAsync(ClinicianProfileRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        if (!await IsClinicianAsync(userId))
        {
            return OperationResult<ClinicianProfileDto>.Fail(403, "Forbidden", "Only a clinician can update this profile.");
        }

        var profile = await _db.ClinicianProfiles.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var created = profile is null;
        profile ??= new ClinicianProfile { UserId = userId };
        profile.FirstName = request.FirstName.Trim();
        profile.LastName = request.LastName.Trim();
        profile.PhoneNumber = request.PhoneNumber.Trim();
        profile.ProfessionalTitle = request.ProfessionalTitle.Trim();
        profile.Specialty = request.Specialty.Trim();
        profile.YearsOfExperience = request.YearsOfExperience;
        profile.PreferredLocations = request.PreferredLocations.Trim();
        profile.EmploymentPreference = Enum.Parse<EmploymentPreference>(request.EmploymentPreference, true);
        profile.AvailabilityDate = request.AvailabilityDate;
        profile.ProfileStatus = ProfileStatus.Active;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        if (created)
        {
            _db.ClinicianProfiles.Add(profile);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(created ? "ClinicianProfileCreated" : "ClinicianProfileUpdated", userId, true, profile.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ClinicianProfileDto>.Ok(await MapOwnedAsync(profile.Id, cancellationToken));
    }

    public async Task<OperationResult<PageDto<ClinicianProfileDto>>> ListAsync(ClinicianListQuery query, string userId, CancellationToken cancellationToken)
    {
        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        if (access.IsClinicianOnly)
        {
            return OperationResult<PageDto<ClinicianProfileDto>>.Fail(403, "Forbidden", "Clinicians cannot browse other profiles.");
        }

        var profiles = _db.ClinicianProfiles.AsNoTracking().AsQueryable();
        if (!access.IsAdministrator && !access.IsRecruiter)
        {
            var visibleIds = await SubmittedProfileIdsAsync(access.ManagedFacilityIds, cancellationToken);
            profiles = profiles.Where(item => visibleIds.Contains(item.Id));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            profiles = profiles.Where(item => item.FirstName.ToLower().Contains(term) || item.LastName.ToLower().Contains(term) || item.Specialty.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.Specialty))
        {
            var specialty = query.Specialty.Trim().ToLower();
            profiles = profiles.Where(item => item.Specialty.ToLower().Contains(specialty));
        }

        if (query.MinExperience is int years)
        {
            profiles = profiles.Where(item => item.YearsOfExperience >= years);
        }

        if (!string.IsNullOrWhiteSpace(query.Location))
        {
            var location = query.Location.Trim().ToLower();
            profiles = profiles.Where(item => item.PreferredLocations.ToLower().Contains(location));
        }

        if (query.AvailableBy is DateOnly availableBy)
        {
            profiles = profiles.Where(item => item.AvailabilityDate != null && item.AvailabilityDate <= availableBy);
        }

        profiles = query.Sort?.ToLowerInvariant() switch
        {
            "name" => profiles.OrderBy(item => item.LastName).ThenBy(item => item.FirstName),
            "experience" => profiles.OrderByDescending(item => item.YearsOfExperience),
            "specialty" => profiles.OrderBy(item => item.Specialty),
            _ => profiles.OrderByDescending(item => item.UpdatedAt)
        };

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 50 ? 10 : query.PageSize;
        var total = await profiles.CountAsync(cancellationToken);
        var items = await profiles.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var revealNumbers = access.IsAdministrator || access.IsRecruiter;
        return OperationResult<PageDto<ClinicianProfileDto>>.Ok(new PageDto<ClinicianProfileDto>(
            items.Select(item => Map(item, revealNumbers)).ToArray(), page, pageSize, total));
    }

    public async Task<OperationResult<ClinicianProfileDto>> GetAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        var profile = await _db.ClinicianProfiles.AsNoTracking().Include(item => item.Credentials)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null)
        {
            return OperationResult<ClinicianProfileDto>.Fail(404, "Not found", "That clinician profile does not exist.");
        }

        if (profile.UserId == userId)
        {
            return OperationResult<ClinicianProfileDto>.Ok(Map(profile, true));
        }

        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        if (access.IsAdministrator || access.IsRecruiter)
        {
            return OperationResult<ClinicianProfileDto>.Ok(Map(profile, true));
        }

        var submitted = await SubmittedProfileIdsAsync(access.ManagedFacilityIds, cancellationToken);
        if (!submitted.Contains(profile.Id))
        {
            return OperationResult<ClinicianProfileDto>.Fail(403, "Forbidden", "You cannot view this clinician.");
        }

        return OperationResult<ClinicianProfileDto>.Ok(Map(profile, false));
    }

    public async Task<OperationResult<CredentialDto>> AddCredentialAsync(CredentialRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var profile = await OwnedProfileAsync(userId, cancellationToken);
        if (profile is null)
        {
            return OperationResult<CredentialDto>.Fail(404, "Not found", "Create your professional profile before adding credentials.");
        }

        var credential = new ClinicianCredential
        {
            ClinicianProfileId = profile.Id,
            CredentialType = Enum.Parse<CredentialType>(request.CredentialType, true),
            CredentialName = request.CredentialName.Trim(),
            IssuingOrganization = request.IssuingOrganization.Trim(),
            CredentialNumber = request.CredentialNumber.Trim(),
            IssueDate = request.IssueDate,
            ExpirationDate = request.ExpirationDate
        };
        _db.ClinicianCredentials.Add(credential);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("CredentialAdded", userId, true, credential.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<CredentialDto>.Ok(MapCredential(credential, true));
    }

    public async Task<OperationResult<IReadOnlyList<CredentialDto>>> ListCredentialsAsync(string userId, CancellationToken cancellationToken)
    {
        var profile = await _db.ClinicianProfiles.AsNoTracking().Include(item => item.Credentials)
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (profile is null)
        {
            return OperationResult<IReadOnlyList<CredentialDto>>.Fail(404, "Not found", "Create your professional profile before adding credentials.");
        }

        return OperationResult<IReadOnlyList<CredentialDto>>.Ok(profile.Credentials.Select(item => MapCredential(item, true)).ToArray());
    }

    public async Task<OperationResult<CredentialDto>> UpdateCredentialAsync(Guid id, CredentialRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var credential = await OwnedCredentialAsync(id, userId, cancellationToken);
        if (credential is null)
        {
            return OperationResult<CredentialDto>.Fail(404, "Not found", "That credential does not exist.");
        }

        if (credential.VerificationStatus == VerificationStatus.Verified)
        {
            return OperationResult<CredentialDto>.Fail(409, "Verified credential", "A verified credential cannot be edited.");
        }

        credential.CredentialType = Enum.Parse<CredentialType>(request.CredentialType, true);
        credential.CredentialName = request.CredentialName.Trim();
        credential.IssuingOrganization = request.IssuingOrganization.Trim();
        credential.CredentialNumber = request.CredentialNumber.Trim();
        credential.IssueDate = request.IssueDate;
        credential.ExpirationDate = request.ExpirationDate;
        credential.VerificationStatus = VerificationStatus.Pending;
        credential.VerifiedBy = null;
        credential.VerifiedAt = null;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("CredentialUpdated", userId, true, credential.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<CredentialDto>.Ok(MapCredential(credential, true));
    }

    public async Task<OperationResult<bool>> DeleteCredentialAsync(Guid id, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var credential = await OwnedCredentialAsync(id, userId, cancellationToken);
        if (credential is null)
        {
            return OperationResult<bool>.Fail(404, "Not found", "That credential does not exist.");
        }

        _db.ClinicianCredentials.Remove(credential);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("CredentialRemoved", userId, true, id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<CredentialDto>> VerifyCredentialAsync(Guid profileId, Guid credentialId, CredentialVerificationRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        if (!access.IsAdministrator && !access.IsRecruiter)
        {
            return OperationResult<CredentialDto>.Fail(403, "Forbidden", "Only a recruiter can verify a credential.");
        }

        var credential = await _db.ClinicianCredentials.FirstOrDefaultAsync(
            item => item.Id == credentialId && item.ClinicianProfileId == profileId, cancellationToken);
        if (credential is null)
        {
            return OperationResult<CredentialDto>.Fail(404, "Not found", "That credential does not exist.");
        }

        credential.VerificationStatus = Enum.Parse<VerificationStatus>(request.VerificationStatus, true);
        credential.VerifiedBy = credential.VerificationStatus == VerificationStatus.Pending ? null : userId;
        credential.VerifiedAt = credential.VerificationStatus == VerificationStatus.Pending ? null : DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("CredentialVerified", userId, true, credential.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<CredentialDto>.Ok(MapCredential(credential, true));
    }

    private async Task<bool> IsClinicianAsync(string userId)
    {
        var user = await _users.FindByIdAsync(userId);
        return user is not null && (await _users.GetRolesAsync(user)).Contains(AppRoles.Clinician, StringComparer.Ordinal);
    }

    private async Task<ClinicianProfile?> OwnedProfileAsync(string userId, CancellationToken cancellationToken) =>
        await _db.ClinicianProfiles.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

    private async Task<ClinicianCredential?> OwnedCredentialAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        var profile = await OwnedProfileAsync(userId, cancellationToken);
        return profile is null
            ? null
            : await _db.ClinicianCredentials.FirstOrDefaultAsync(item => item.Id == id && item.ClinicianProfileId == profile.Id, cancellationToken);
    }

    private async Task<HashSet<Guid>> SubmittedProfileIdsAsync(IReadOnlyCollection<Guid> facilityIds, CancellationToken cancellationToken)
    {
        var submitted = new[]
        {
            ApplicationStatus.SubmittedToFacility, ApplicationStatus.Interview, ApplicationStatus.Offer,
            ApplicationStatus.Placed, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn
        };
        var ids = await (
            from application in _db.JobApplications.AsNoTracking()
            join requisition in _db.JobRequisitions.AsNoTracking() on application.JobRequisitionId equals requisition.Id
            where facilityIds.Contains(requisition.FacilityId) && submitted.Contains(application.Status)
            select application.ClinicianProfileId).ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    private async Task<ClinicianProfileDto> MapOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await _db.ClinicianProfiles.AsNoTracking().Include(item => item.Credentials)
            .FirstAsync(item => item.Id == id, cancellationToken);
        return Map(profile, true);
    }

    private static ClinicianProfileDto Map(ClinicianProfile profile, bool revealNumbers) => new(
        profile.Id,
        profile.UserId,
        profile.FirstName,
        profile.LastName,
        profile.PhoneNumber,
        profile.ProfessionalTitle,
        profile.Specialty,
        profile.YearsOfExperience,
        profile.PreferredLocations,
        profile.EmploymentPreference.ToString(),
        profile.AvailabilityDate,
        profile.ProfileStatus.ToString(),
        profile.CreatedAt,
        profile.UpdatedAt,
        profile.Credentials.Select(item => MapCredential(item, revealNumbers)).ToArray());

    private static CredentialDto MapCredential(ClinicianCredential credential, bool revealNumber) => new(
        credential.Id,
        credential.CredentialType.ToString(),
        credential.CredentialName,
        credential.IssuingOrganization,
        revealNumber ? credential.CredentialNumber : null,
        credential.IssueDate,
        credential.ExpirationDate,
        credential.VerificationStatus.ToString(),
        credential.VerifiedAt);
}

public sealed class ApplicationService : IApplicationService
{
    private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> Forward = new()
    {
        [ApplicationStatus.Applied] = [ApplicationStatus.UnderReview, ApplicationStatus.Rejected],
        [ApplicationStatus.UnderReview] = [ApplicationStatus.Screening, ApplicationStatus.Rejected],
        [ApplicationStatus.Screening] = [ApplicationStatus.SubmittedToFacility, ApplicationStatus.Rejected],
        [ApplicationStatus.SubmittedToFacility] = [ApplicationStatus.Interview, ApplicationStatus.Rejected],
        [ApplicationStatus.Interview] = [ApplicationStatus.Offer, ApplicationStatus.Rejected],
        [ApplicationStatus.Offer] = [ApplicationStatus.Rejected],
        [ApplicationStatus.Placed] = [],
        [ApplicationStatus.Rejected] = [],
        [ApplicationStatus.Withdrawn] = []
    };

    private static readonly ApplicationStatus[] FacilityVisible =
    [
        ApplicationStatus.SubmittedToFacility, ApplicationStatus.Interview, ApplicationStatus.Offer,
        ApplicationStatus.Placed, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn
    ];

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditWriter _audit;

    public ApplicationService(ApplicationDbContext db, UserManager<ApplicationUser> users, IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    public async Task<OperationResult<ApplicationDto>> ApplyAsync(ApplicationRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null || !(await _users.GetRolesAsync(user)).Contains(AppRoles.Clinician, StringComparer.Ordinal))
        {
            return OperationResult<ApplicationDto>.Fail(403, "Forbidden", "Only a clinician can apply for a position.");
        }

        var profile = await _db.ClinicianProfiles.FirstOrDefaultAsync(item => item.UserId == userId && item.ProfileStatus == ProfileStatus.Active, cancellationToken);
        if (profile is null)
        {
            return OperationResult<ApplicationDto>.Fail(409, "Profile required", "Complete your professional profile before applying.");
        }

        var requisition = await _db.JobRequisitions.FirstOrDefaultAsync(item => item.Id == request.JobRequisitionId, cancellationToken);
        if (requisition is null || requisition.Status != RequisitionStatus.Published || requisition.OpenPositions < 1)
        {
            return OperationResult<ApplicationDto>.Fail(409, "Unavailable", "This position is not open for applications.");
        }

        if (await _db.JobApplications.AnyAsync(item => item.JobRequisitionId == requisition.Id && item.ClinicianProfileId == profile.Id, cancellationToken))
        {
            return OperationResult<ApplicationDto>.Fail(409, "Duplicate application", "You already applied for this position.");
        }

        var application = new JobApplication
        {
            JobRequisitionId = requisition.Id,
            ClinicianProfileId = profile.Id,
            RecruiterId = requisition.AssignedRecruiterId,
            History = [new ApplicationStatusHistory { NewStatus = ApplicationStatus.Applied, ChangedBy = userId, Reason = "Application submitted" }]
        };
        _db.JobApplications.Add(application);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ApplicationSubmitted", userId, true, application.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ApplicationDto>.Ok((await ProjectAsync([application.Id], cancellationToken)).Single());
    }

    public Task<OperationResult<PageDto<ApplicationDto>>> ListMineAsync(ApplicationListQuery query, string userId, CancellationToken cancellationToken) =>
        ListInternalAsync(query, userId, mine: true, cancellationToken);

    public Task<OperationResult<PageDto<ApplicationDto>>> ListAsync(ApplicationListQuery query, string userId, CancellationToken cancellationToken) =>
        ListInternalAsync(query, userId, mine: false, cancellationToken);

    public async Task<OperationResult<ApplicationDto>> GetAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        var visible = await VisibleIdsAsync(userId, cancellationToken);
        if (!visible.Contains(id))
        {
            var exists = await _db.JobApplications.AnyAsync(item => item.Id == id, cancellationToken);
            return exists
                ? OperationResult<ApplicationDto>.Fail(403, "Forbidden", "You cannot view this application.")
                : OperationResult<ApplicationDto>.Fail(404, "Not found", "That application does not exist.");
        }

        return OperationResult<ApplicationDto>.Ok((await ProjectAsync([id], cancellationToken)).Single());
    }

    public async Task<OperationResult<ApplicationDto>> ChangeStatusAsync(Guid id, ApplicationStatusRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var application = await _db.JobApplications.Include(item => item.Profile)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (application is null)
        {
            return OperationResult<ApplicationDto>.Fail(404, "Not found", "That application does not exist.");
        }

        var requisition = await _db.JobRequisitions.FirstAsync(item => item.Id == application.JobRequisitionId, cancellationToken);
        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        var next = Enum.Parse<ApplicationStatus>(request.Status, true);
        var owns = application.Profile.UserId == userId;
        var recruits = access.IsAdministrator || (access.IsRecruiter && (requisition.AssignedRecruiterId == userId || application.RecruiterId == userId));
        var manages = access.ManagedFacilityIds.Contains(requisition.FacilityId);
        var facilityStage = application.Status is ApplicationStatus.SubmittedToFacility or ApplicationStatus.Interview;

        if (next == application.Status)
        {
            if (!recruits && !manages)
            {
                return OperationResult<ApplicationDto>.Fail(403, "Forbidden", "You cannot update this application.");
            }

            application.Notes = request.Notes?.Trim() ?? application.Notes;
            application.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return OperationResult<ApplicationDto>.Ok((await ProjectAsync([application.Id], cancellationToken)).Single());
        }

        if (next == ApplicationStatus.Placed)
        {
            return OperationResult<ApplicationDto>.Fail(409, "Invalid status", "Create a placement to record a hire.");
        }

        var withdrawing = next == ApplicationStatus.Withdrawn
            && owns
            && application.Status is not ApplicationStatus.Placed
            && application.Status is not ApplicationStatus.Rejected
            && application.Status is not ApplicationStatus.Withdrawn;
        var recruiting = recruits && Forward[application.Status].Contains(next);
        var reviewing = manages && facilityStage && Forward[application.Status].Contains(next);
        if (!withdrawing && !recruiting && !reviewing)
        {
            var involved = owns || recruits || manages;
            return OperationResult<ApplicationDto>.Fail(involved ? 409 : 403, "Invalid status", "That application status change is not allowed.");
        }

        var previous = application.Status;
        application.Status = next;
        application.Notes = request.Notes?.Trim() ?? application.Notes;
        application.UpdatedAt = DateTimeOffset.UtcNow;
        if (recruits && application.RecruiterId is null)
        {
            application.RecruiterId = userId;
        }

        _db.ApplicationStatusHistory.Add(new ApplicationStatusHistory
        {
            JobApplicationId = application.Id,
            PreviousStatus = previous,
            NewStatus = next,
            ChangedBy = userId,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "" : request.Reason.Trim()
        });
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ApplicationStatusChanged", userId, true, $"{application.Id}:{next}", ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ApplicationDto>.Ok((await ProjectAsync([application.Id], cancellationToken)).Single());
    }

    public async Task<OperationResult<RecruitmentSummaryDto>> SummaryAsync(string userId, CancellationToken cancellationToken)
    {
        var ids = await VisibleIdsAsync(userId, cancellationToken);
        var applications = await _db.JobApplications.AsNoTracking().Where(item => ids.Contains(item.Id)).ToListAsync(cancellationToken);
        var placements = await _db.Placements.AsNoTracking().CountAsync(
            item => item.Status == PlacementStatus.Active && applications.Select(application => application.Id).Contains(item.JobApplicationId),
            cancellationToken);
        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        var requisitions = _db.JobRequisitions.AsNoTracking().Where(item => item.Status == RequisitionStatus.Published);
        if (!access.IsAdministrator && !access.IsClinicianOnly)
        {
            requisitions = requisitions.Where(item => access.ManagedFacilityIds.Contains(item.FacilityId) || item.AssignedRecruiterId == userId);
        }

        var pipeline = applications.GroupBy(item => item.Status.ToString())
            .Select(group => new StatusCountDto(group.Key, group.Count()))
            .OrderBy(item => item.Status)
            .ToArray();
        return OperationResult<RecruitmentSummaryDto>.Ok(new RecruitmentSummaryDto(
            applications.Count, placements, await requisitions.CountAsync(cancellationToken), pipeline));
    }

    private async Task<OperationResult<PageDto<ApplicationDto>>> ListInternalAsync(ApplicationListQuery query, string userId, bool mine, CancellationToken cancellationToken)
    {
        var ids = await VisibleIdsAsync(userId, cancellationToken);
        var applications = _db.JobApplications.AsNoTracking().Where(item => ids.Contains(item.Id));
        if (mine)
        {
            var profileId = await _db.ClinicianProfiles.Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).FirstOrDefaultAsync(cancellationToken);
            applications = applications.Where(item => item.ClinicianProfileId == profileId);
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<ApplicationStatus>(query.Status, true, out var status))
        {
            applications = applications.Where(item => item.Status == status);
        }

        if (query.FacilityId is Guid facilityId)
        {
            var requisitionIds = _db.JobRequisitions.Where(item => item.FacilityId == facilityId).Select(item => item.Id);
            applications = applications.Where(item => requisitionIds.Contains(item.JobRequisitionId));
        }

        if (!string.IsNullOrWhiteSpace(query.Specialty) || !string.IsNullOrWhiteSpace(query.Search))
        {
            var specialty = query.Specialty?.Trim().ToLower();
            var search = query.Search?.Trim().ToLower();
            var matching = _db.JobRequisitions.Where(item =>
                (specialty == null || item.Specialty.ToLower().Contains(specialty))
                && (search == null || item.JobTitle.ToLower().Contains(search) || item.Specialty.ToLower().Contains(search)));
            if (!string.IsNullOrWhiteSpace(search))
            {
                var profileIds = _db.ClinicianProfiles.Where(item => item.FirstName.ToLower().Contains(search) || item.LastName.ToLower().Contains(search)).Select(item => item.Id);
                applications = applications.Where(item => matching.Select(requisition => requisition.Id).Contains(item.JobRequisitionId) || profileIds.Contains(item.ClinicianProfileId));
            }
            else
            {
                applications = applications.Where(item => matching.Select(requisition => requisition.Id).Contains(item.JobRequisitionId));
            }
        }

        applications = query.Sort?.ToLowerInvariant() switch
        {
            "status" => applications.OrderBy(item => item.Status),
            "applied" => applications.OrderBy(item => item.AppliedAt),
            _ => applications.OrderByDescending(item => item.UpdatedAt)
        };
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 50 ? 10 : query.PageSize;
        var total = await applications.CountAsync(cancellationToken);
        var pageIds = await applications.Skip((page - 1) * pageSize).Take(pageSize).Select(item => item.Id).ToListAsync(cancellationToken);
        return OperationResult<PageDto<ApplicationDto>>.Ok(new PageDto<ApplicationDto>(await ProjectAsync(pageIds, cancellationToken), page, pageSize, total));
    }

    private async Task<HashSet<Guid>> VisibleIdsAsync(string userId, CancellationToken cancellationToken)
    {
        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        var query = from application in _db.JobApplications.AsNoTracking()
                    join requisition in _db.JobRequisitions.AsNoTracking() on application.JobRequisitionId equals requisition.Id
                    select new { application.Id, application.Status, application.ClinicianProfileId, application.RecruiterId, requisition.FacilityId, requisition.AssignedRecruiterId };
        if (access.IsAdministrator)
        {
            return (await query.Select(item => item.Id).ToListAsync(cancellationToken)).ToHashSet();
        }

        var profileId = await _db.ClinicianProfiles.Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).FirstOrDefaultAsync(cancellationToken);
        var ids = await query.Where(item =>
                item.ClinicianProfileId == profileId
                || (access.IsRecruiter && (item.AssignedRecruiterId == userId || item.RecruiterId == userId))
                || (access.ManagedFacilityIds.Contains(item.FacilityId) && FacilityVisible.Contains(item.Status)))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    private async Task<IReadOnlyList<ApplicationDto>> ProjectAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var rows = await (
            from application in _db.JobApplications.AsNoTracking().Include(item => item.History)
            join requisition in _db.JobRequisitions.AsNoTracking() on application.JobRequisitionId equals requisition.Id
            join facility in _db.HealthcareFacilities.AsNoTracking() on requisition.FacilityId equals facility.Id
            join profile in _db.ClinicianProfiles.AsNoTracking() on application.ClinicianProfileId equals profile.Id
            where ids.Contains(application.Id)
            select new { application, requisition, facility, profile }).ToListAsync(cancellationToken);
        return ids.Select(id => rows.First(row => row.application.Id == id))
            .Select(row => new ApplicationDto(
                row.application.Id,
                row.requisition.Id,
                row.requisition.JobTitle,
                row.requisition.Specialty,
                row.facility.Id,
                row.facility.Name,
                row.profile.Id,
                $"{row.profile.FirstName} {row.profile.LastName}",
                row.application.Status.ToString(),
                row.application.RecruiterId,
                row.application.Notes,
                row.application.AppliedAt,
                row.application.UpdatedAt,
                row.application.History.OrderBy(item => item.ChangedAt).Select(item => new ApplicationHistoryDto(
                    item.Id, item.PreviousStatus?.ToString(), item.NewStatus.ToString(), item.ChangedAt, item.Reason)).ToArray()))
            .ToArray();
    }
}

public sealed class PlacementService : IPlacementService
{
    private static readonly Dictionary<PlacementStatus, PlacementStatus[]> Forward = new()
    {
        [PlacementStatus.Pending] = [PlacementStatus.Active, PlacementStatus.Cancelled],
        [PlacementStatus.Active] = [PlacementStatus.Completed, PlacementStatus.Cancelled],
        [PlacementStatus.Completed] = [],
        [PlacementStatus.Cancelled] = []
    };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditWriter _audit;

    public PlacementService(ApplicationDbContext db, UserManager<ApplicationUser> users, IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    public async Task<OperationResult<PlacementDto>> CreateAsync(PlacementRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var application = await _db.JobApplications.Include(item => item.Profile)
            .FirstOrDefaultAsync(item => item.Id == request.JobApplicationId, cancellationToken);
        if (application is null)
        {
            return OperationResult<PlacementDto>.Fail(404, "Not found", "That application does not exist.");
        }

        var requisition = await _db.JobRequisitions.AsNoTracking().FirstAsync(item => item.Id == application.JobRequisitionId, cancellationToken);
        if (!await CanPlaceAsync(userId, requisition, application))
        {
            return OperationResult<PlacementDto>.Fail(403, "Forbidden", "You cannot place this candidate.");
        }

        if (application.Status != ApplicationStatus.Offer)
        {
            return OperationResult<PlacementDto>.Fail(409, "Offer required", "Record an offer before creating a placement.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var reserved = await _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "JobRequisitions"
            SET "OpenPositions" = "OpenPositions" - 1,
                "Status" = CASE WHEN "OpenPositions" - 1 = 0 THEN 'Filled' ELSE "Status" END,
                "UpdatedAt" = {now}
            WHERE "Id" = {requisition.Id}
              AND "OpenPositions" > 0
              AND "Status" IN ('Published', 'Open')
            """, cancellationToken);
        if (reserved != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult<PlacementDto>.Fail(409, "No positions", "This requisition is closed or has no remaining positions.");
        }

        var previous = application.Status;
        application.Status = ApplicationStatus.Placed;
        application.UpdatedAt = now;
        _db.ApplicationStatusHistory.Add(new ApplicationStatusHistory
        {
            JobApplicationId = application.Id,
            PreviousStatus = previous,
            NewStatus = ApplicationStatus.Placed,
            ChangedBy = userId,
            Reason = "Placement created"
        });
        var placement = new Placement
        {
            JobApplicationId = application.Id,
            FacilityId = requisition.FacilityId,
            ClinicianProfileId = application.ClinicianProfileId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            AgreedPayRate = request.AgreedPayRate,
            Status = PlacementStatus.Active
        };
        _db.Placements.Add(placement);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("PlacementCreated", userId, true, placement.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult<PlacementDto>.Ok((await ProjectAsync([placement.Id], cancellationToken)).Single());
    }

    public async Task<OperationResult<PageDto<PlacementDto>>> ListAsync(PlacementListQuery query, string userId, CancellationToken cancellationToken)
    {
        var placements = await VisibleAsync(userId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<PlacementStatus>(query.Status, true, out var status))
        {
            placements = placements.Where(item => item.Status == status);
        }

        if (query.FacilityId is Guid facilityId)
        {
            placements = placements.Where(item => item.FacilityId == facilityId);
        }

        placements = query.Sort?.ToLowerInvariant() switch
        {
            "start" => placements.OrderBy(item => item.StartDate),
            "status" => placements.OrderBy(item => item.Status),
            _ => placements.OrderByDescending(item => item.CreatedAt)
        };
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 50 ? 10 : query.PageSize;
        var total = await placements.CountAsync(cancellationToken);
        var ids = await placements.Skip((page - 1) * pageSize).Take(pageSize).Select(item => item.Id).ToListAsync(cancellationToken);
        return OperationResult<PageDto<PlacementDto>>.Ok(new PageDto<PlacementDto>(await ProjectAsync(ids, cancellationToken), page, pageSize, total));
    }

    public async Task<OperationResult<PlacementDto>> GetAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        var visible = await VisibleAsync(userId, cancellationToken);
        if (!await visible.AnyAsync(item => item.Id == id, cancellationToken))
        {
            var exists = await _db.Placements.AnyAsync(item => item.Id == id, cancellationToken);
            return exists
                ? OperationResult<PlacementDto>.Fail(403, "Forbidden", "You cannot view this placement.")
                : OperationResult<PlacementDto>.Fail(404, "Not found", "That placement does not exist.");
        }

        return OperationResult<PlacementDto>.Ok((await ProjectAsync([id], cancellationToken)).Single());
    }

    public async Task<OperationResult<PlacementDto>> ChangeStatusAsync(Guid id, PlacementStatusRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var placement = await _db.Placements.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (placement is null)
        {
            return OperationResult<PlacementDto>.Fail(404, "Not found", "That placement does not exist.");
        }

        if (!await (await VisibleAsync(userId, cancellationToken)).AnyAsync(item => item.Id == id, cancellationToken))
        {
            return OperationResult<PlacementDto>.Fail(403, "Forbidden", "You cannot update this placement.");
        }

        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        var application = await _db.JobApplications.AsNoTracking().FirstAsync(item => item.Id == placement.JobApplicationId, cancellationToken);
        var requisition = await _db.JobRequisitions.AsNoTracking().FirstAsync(item => item.Id == application.JobRequisitionId, cancellationToken);
        var canChange = access.IsAdministrator
            || access.ManagedFacilityIds.Contains(placement.FacilityId)
            || (access.IsRecruiter && requisition.AssignedRecruiterId == userId);
        if (!canChange)
        {
            return OperationResult<PlacementDto>.Fail(403, "Forbidden", "You cannot update this placement.");
        }

        var next = Enum.Parse<PlacementStatus>(request.Status, true);
        if (next != placement.Status && !Forward[placement.Status].Contains(next))
        {
            return OperationResult<PlacementDto>.Fail(409, "Invalid status", $"A placement cannot move from {placement.Status} to {next}.");
        }

        placement.Status = next;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("PlacementStatusChanged", userId, true, $"{placement.Id}:{next}", ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<PlacementDto>.Ok((await ProjectAsync([placement.Id], cancellationToken)).Single());
    }

    private async Task<bool> CanPlaceAsync(string userId, JobRequisition requisition, JobApplication application)
    {
        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, CancellationToken.None);
        return access.IsAdministrator
            || access.ManagedFacilityIds.Contains(requisition.FacilityId)
            || (access.IsRecruiter && (requisition.AssignedRecruiterId == userId || application.RecruiterId == userId));
    }

    private async Task<IQueryable<Placement>> VisibleAsync(string userId, CancellationToken cancellationToken)
    {
        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        var query = _db.Placements.AsNoTracking();
        if (access.IsAdministrator)
        {
            return query;
        }

        var profileId = await _db.ClinicianProfiles.Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).FirstOrDefaultAsync(cancellationToken);
        var assigned = _db.JobRequisitions.Where(item => item.AssignedRecruiterId == userId).Select(item => item.Id);
        var applicationIds = _db.JobApplications.Where(item => assigned.Contains(item.JobRequisitionId) || item.RecruiterId == userId).Select(item => item.Id);
        return query.Where(item =>
            item.ClinicianProfileId == profileId
            || access.ManagedFacilityIds.Contains(item.FacilityId)
            || (access.IsRecruiter && applicationIds.Contains(item.JobApplicationId)));
    }

    private async Task<IReadOnlyList<PlacementDto>> ProjectAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var rows = await (
            from placement in _db.Placements.AsNoTracking()
            join application in _db.JobApplications.AsNoTracking() on placement.JobApplicationId equals application.Id
            join requisition in _db.JobRequisitions.AsNoTracking() on application.JobRequisitionId equals requisition.Id
            join facility in _db.HealthcareFacilities.AsNoTracking() on placement.FacilityId equals facility.Id
            join profile in _db.ClinicianProfiles.AsNoTracking() on placement.ClinicianProfileId equals profile.Id
            where ids.Contains(placement.Id)
            select new { placement, requisition, facility, profile }).ToListAsync(cancellationToken);
        return ids.Select(id => rows.First(row => row.placement.Id == id))
            .Select(row => new PlacementDto(
                row.placement.Id,
                row.placement.JobApplicationId,
                row.facility.Id,
                row.facility.Name,
                row.profile.Id,
                $"{row.profile.FirstName} {row.profile.LastName}",
                row.requisition.JobTitle,
                row.placement.StartDate,
                row.placement.EndDate,
                row.placement.AgreedPayRate,
                row.placement.Status.ToString(),
                row.placement.CreatedAt))
            .ToArray();
    }
}
