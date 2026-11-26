using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartStaff.Application.Authentication;
using SmartStaff.Application.Common;
using SmartStaff.Application.Staffing;
using SmartStaff.Domain.Staffing;
using SmartStaff.Infrastructure.Identity;
using SmartStaff.Infrastructure.Persistence;

namespace SmartStaff.Infrastructure.Staffing;

public sealed class FacilityService : IFacilityService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditWriter _audit;

    public FacilityService(ApplicationDbContext db, UserManager<ApplicationUser> users, IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    public async Task<OperationResult<FacilityDto>> CreateAsync(FacilityRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        if (!await StaffingAccess.IsAdministratorAsync(_users, userId))
        {
            return OperationResult<FacilityDto>.Fail(403, "Forbidden", "Only an administrator can create a facility.");
        }

        var now = DateTimeOffset.UtcNow;
        var facility = new HealthcareFacility
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            FacilityType = Enum.Parse<FacilityType>(request.FacilityType, true),
            Description = Clean(request.Description),
            Address = request.Address.Trim(),
            City = request.City.Trim(),
            State = request.State.Trim().ToUpperInvariant(),
            ZipCode = request.ZipCode.Trim(),
            ContactEmail = request.ContactEmail.Trim(),
            ContactPhone = request.ContactPhone.Trim(),
            Status = ParseStatus(request.Status),
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.HealthcareFacilities.Add(facility);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("FacilityCreated", userId, true, facility.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<FacilityDto>.Ok(Map(facility));
    }

    public async Task<OperationResult<PageDto<FacilityDto>>> ListAsync(FacilityListQuery query, string userId, CancellationToken cancellationToken)
    {
        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        if (access.IsClinicianOnly)
        {
            return OperationResult<PageDto<FacilityDto>>.Fail(403, "Forbidden", "Clinicians cannot browse the facility directory.");
        }

        var facilities = _db.HealthcareFacilities.AsNoTracking().AsQueryable();
        if (!access.IsAdministrator)
        {
            var allowed = access.ManagedFacilityIds.Concat(access.RecruiterFacilityIds).Distinct().ToArray();
            facilities = facilities.Where(facility => allowed.Contains(facility.Id));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            facilities = facilities.Where(facility =>
                facility.Name.ToLower().Contains(search)
                || facility.City.ToLower().Contains(search)
                || facility.ContactEmail.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim().ToLower();
            facilities = facilities.Where(facility => facility.City.ToLower() == city);
        }

        if (!string.IsNullOrWhiteSpace(query.State))
        {
            var state = query.State.Trim().ToUpper();
            facilities = facilities.Where(facility => facility.State == state);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<FacilityStatus>(query.Status, true, out var status))
            {
                return OperationResult<PageDto<FacilityDto>>.Fail(400, "Invalid filter", "Choose a valid facility status.");
            }

            facilities = facilities.Where(facility => facility.Status == status);
        }

        facilities = query.Sort?.ToLowerInvariant() switch
        {
            "name" => facilities.OrderBy(facility => facility.Name),
            "-name" => facilities.OrderByDescending(facility => facility.Name),
            "city" => facilities.OrderBy(facility => facility.City),
            _ => facilities.OrderByDescending(facility => facility.CreatedAt)
        };

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 50 ? 10 : query.PageSize;
        var total = await facilities.CountAsync(cancellationToken);
        var items = await facilities.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return OperationResult<PageDto<FacilityDto>>.Ok(new PageDto<FacilityDto>(items.Select(Map).ToArray(), page, pageSize, total));
    }

    public async Task<OperationResult<FacilityDto>> GetAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        var facility = await _db.HealthcareFacilities.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (facility is null)
        {
            return OperationResult<FacilityDto>.Fail(404, "Not found", "That facility does not exist.");
        }

        if (!await StaffingAccess.CanViewFacilityAsync(_users, _db, userId, id, cancellationToken))
        {
            return OperationResult<FacilityDto>.Fail(403, "Forbidden", "You cannot view this facility.");
        }

        return OperationResult<FacilityDto>.Ok(Map(facility));
    }

    public async Task<OperationResult<FacilityDto>> UpdateAsync(Guid id, FacilityRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var facility = await _db.HealthcareFacilities.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (facility is null)
        {
            return OperationResult<FacilityDto>.Fail(404, "Not found", "That facility does not exist.");
        }

        if (!await StaffingAccess.CanManageFacilityAsync(_users, _db, userId, id, cancellationToken))
        {
            return OperationResult<FacilityDto>.Fail(403, "Forbidden", "You cannot update this facility.");
        }

        facility.Name = request.Name.Trim();
        facility.FacilityType = Enum.Parse<FacilityType>(request.FacilityType, true);
        facility.Description = Clean(request.Description);
        facility.Address = request.Address.Trim();
        facility.City = request.City.Trim();
        facility.State = request.State.Trim().ToUpperInvariant();
        facility.ZipCode = request.ZipCode.Trim();
        facility.ContactEmail = request.ContactEmail.Trim();
        facility.ContactPhone = request.ContactPhone.Trim();
        facility.Status = ParseStatus(request.Status);
        facility.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("FacilityUpdated", userId, true, facility.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<FacilityDto>.Ok(Map(facility));
    }

    public async Task<OperationResult<IReadOnlyList<LocationDto>>> ListLocationsAsync(Guid facilityId, string userId, CancellationToken cancellationToken)
    {
        if (!await _db.HealthcareFacilities.AnyAsync(item => item.Id == facilityId, cancellationToken))
        {
            return OperationResult<IReadOnlyList<LocationDto>>.Fail(404, "Not found", "That facility does not exist.");
        }

        if (!await StaffingAccess.CanViewFacilityAsync(_users, _db, userId, facilityId, cancellationToken))
        {
            return OperationResult<IReadOnlyList<LocationDto>>.Fail(403, "Forbidden", "You cannot view this facility.");
        }

        var locations = await _db.FacilityLocations.AsNoTracking()
            .Where(location => location.FacilityId == facilityId)
            .OrderBy(location => location.LocationName)
            .ToListAsync(cancellationToken);
        return OperationResult<IReadOnlyList<LocationDto>>.Ok(locations.Select(MapLocation).ToArray());
    }

    public async Task<OperationResult<LocationDto>> AddLocationAsync(Guid facilityId, LocationRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        if (!await _db.HealthcareFacilities.AnyAsync(item => item.Id == facilityId, cancellationToken))
        {
            return OperationResult<LocationDto>.Fail(404, "Not found", "That facility does not exist.");
        }

        if (!await StaffingAccess.CanManageFacilityAsync(_users, _db, userId, facilityId, cancellationToken))
        {
            return OperationResult<LocationDto>.Fail(403, "Forbidden", "You cannot add a location to this facility.");
        }

        var location = new FacilityLocation
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            LocationName = request.LocationName.Trim(),
            Address = request.Address.Trim(),
            City = request.City.Trim(),
            State = request.State.Trim().ToUpperInvariant(),
            ZipCode = request.ZipCode.Trim(),
            IsActive = request.IsActive ?? true
        };
        _db.FacilityLocations.Add(location);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("LocationCreated", userId, true, location.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<LocationDto>.Ok(MapLocation(location));
    }

    public async Task<OperationResult<MembershipDto>> AddMembershipAsync(Guid facilityId, MembershipRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        if (!await StaffingAccess.IsAdministratorAsync(_users, userId))
        {
            return OperationResult<MembershipDto>.Fail(403, "Forbidden", "Only an administrator can assign facility access.");
        }

        if (!await _db.HealthcareFacilities.AnyAsync(item => item.Id == facilityId, cancellationToken))
        {
            return OperationResult<MembershipDto>.Fail(404, "Not found", "That facility does not exist.");
        }

        var member = await _users.FindByIdAsync(request.UserId) ?? await _users.FindByEmailAsync(request.UserId.Trim());
        if (member is null || !member.IsActive)
        {
            return OperationResult<MembershipDto>.Fail(404, "Not found", "That user account is not available.");
        }

        var role = Enum.Parse<MembershipRole>(request.MembershipRole, true);
        var roles = await _users.GetRolesAsync(member);
        var allowed = role == MembershipRole.Manager
            ? roles.Any(name => AppRoles.FacilityManagerAccess.Contains(name, StringComparer.Ordinal) && name != AppRoles.Administrator)
            : roles.Any(name => AppRoles.RecruiterAccess.Contains(name, StringComparer.Ordinal) && name != AppRoles.Administrator);
        if (!allowed)
        {
            return OperationResult<MembershipDto>.Fail(409, "Role mismatch", "The account does not have the role required for this membership.");
        }

        var existing = await _db.FacilityMemberships.FirstOrDefaultAsync(
            item => item.FacilityId == facilityId && item.UserId == member.Id,
            cancellationToken);
        if (existing is not null)
        {
            existing.MembershipRole = role;
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("MembershipAssigned", userId, true, $"{facilityId}:{role}", ipAddress, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return OperationResult<MembershipDto>.Ok(MapMembership(existing));
        }

        var membership = new FacilityMembership
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            UserId = member.Id,
            MembershipRole = role,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.FacilityMemberships.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("MembershipAssigned", userId, true, $"{facilityId}:{role}", ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<MembershipDto>.Ok(MapMembership(membership));
    }

    private static FacilityStatus ParseStatus(string? status) =>
        string.IsNullOrWhiteSpace(status) ? FacilityStatus.Active : Enum.Parse<FacilityStatus>(status, true);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static FacilityDto Map(HealthcareFacility facility) => new(
        facility.Id,
        facility.Name,
        facility.FacilityType.ToString(),
        facility.Description,
        facility.Address,
        facility.City,
        facility.State,
        facility.ZipCode,
        facility.ContactEmail,
        facility.ContactPhone,
        facility.Status.ToString(),
        facility.CreatedAt,
        facility.UpdatedAt);

    private static LocationDto MapLocation(FacilityLocation location) => new(
        location.Id,
        location.FacilityId,
        location.LocationName,
        location.Address,
        location.City,
        location.State,
        location.ZipCode,
        location.IsActive);

    private static MembershipDto MapMembership(FacilityMembership membership) => new(
        membership.Id,
        membership.FacilityId,
        membership.UserId,
        membership.MembershipRole.ToString(),
        membership.CreatedAt);
}

public sealed class RequisitionService : IRequisitionService
{
    private static readonly Dictionary<RequisitionStatus, RequisitionStatus[]> Transitions = new()
    {
        [RequisitionStatus.Draft] = [RequisitionStatus.Open, RequisitionStatus.Cancelled],
        [RequisitionStatus.Open] = [RequisitionStatus.Published, RequisitionStatus.OnHold, RequisitionStatus.Closed, RequisitionStatus.Cancelled],
        [RequisitionStatus.Published] = [RequisitionStatus.OnHold, RequisitionStatus.Filled, RequisitionStatus.Closed, RequisitionStatus.Cancelled],
        [RequisitionStatus.OnHold] = [RequisitionStatus.Open, RequisitionStatus.Published, RequisitionStatus.Closed, RequisitionStatus.Cancelled],
        [RequisitionStatus.Filled] = [RequisitionStatus.Closed],
        [RequisitionStatus.Closed] = [],
        [RequisitionStatus.Cancelled] = []
    };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditWriter _audit;

    public RequisitionService(ApplicationDbContext db, UserManager<ApplicationUser> users, IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    public async Task<OperationResult<RequisitionDto>> CreateAsync(RequisitionRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        if (!await StaffingAccess.CanManageFacilityAsync(_users, _db, userId, request.FacilityId, cancellationToken))
        {
            return OperationResult<RequisitionDto>.Fail(403, "Forbidden", "You cannot create a requisition for this facility.");
        }

        var location = await _db.FacilityLocations.FirstOrDefaultAsync(
            item => item.Id == request.LocationId && item.FacilityId == request.FacilityId && item.IsActive,
            cancellationToken);
        if (location is null)
        {
            return OperationResult<RequisitionDto>.Fail(400, "Invalid location", "Choose an active location that belongs to the facility.");
        }

        var now = DateTimeOffset.UtcNow;
        var requisition = new JobRequisition
        {
            Id = Guid.NewGuid(),
            FacilityId = request.FacilityId,
            LocationId = request.LocationId,
            JobTitle = request.JobTitle.Trim(),
            Description = request.Description.Trim(),
            Specialty = request.Specialty.Trim(),
            EmploymentType = Enum.Parse<EmploymentType>(request.EmploymentType, true),
            ShiftType = Enum.Parse<ShiftType>(request.ShiftType, true),
            RequiredExperience = request.RequiredExperience,
            RequiredCertifications = request.RequiredCertifications.Trim(),
            OpenPositions = request.OpenPositions,
            PayRateMin = request.PayRateMin,
            PayRateMax = request.PayRateMax,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = RequisitionStatus.Draft,
            CreatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.JobRequisitions.Add(requisition);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("RequisitionCreated", userId, true, requisition.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<RequisitionDto>.Ok(await MapAsync(requisition.Id, cancellationToken));
    }

    public async Task<OperationResult<PageDto<RequisitionDto>>> ListAsync(RequisitionListQuery query, string userId, CancellationToken cancellationToken)
    {
        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        var requisitions = Visible(access, userId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            requisitions = requisitions.Where(item =>
                item.JobTitle.ToLower().Contains(search)
                || item.Specialty.ToLower().Contains(search)
                || item.Facility.Name.ToLower().Contains(search)
                || item.Location.City.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<RequisitionStatus>(query.Status, true, out var status))
            {
                return OperationResult<PageDto<RequisitionDto>>.Fail(400, "Invalid filter", "Choose a valid requisition status.");
            }

            if (access.IsClinicianOnly && status != RequisitionStatus.Published)
            {
                return OperationResult<PageDto<RequisitionDto>>.Ok(new PageDto<RequisitionDto>([], 1, 10, 0));
            }

            requisitions = requisitions.Where(item => item.Status == status);
        }

        if (query.FacilityId is Guid facilityId)
        {
            requisitions = requisitions.Where(item => item.FacilityId == facilityId);
        }

        if (!string.IsNullOrWhiteSpace(query.Specialty))
        {
            var specialty = query.Specialty.Trim().ToLower();
            requisitions = requisitions.Where(item => item.Specialty.ToLower().Contains(specialty));
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim().ToLower();
            requisitions = requisitions.Where(item => item.Location.City.ToLower() == city);
        }

        if (!string.IsNullOrWhiteSpace(query.State))
        {
            var state = query.State.Trim().ToUpper();
            requisitions = requisitions.Where(item => item.Location.State == state);
        }

        if (!string.IsNullOrWhiteSpace(query.ShiftType))
        {
            if (!Enum.TryParse<ShiftType>(query.ShiftType, true, out var shift))
            {
                return OperationResult<PageDto<RequisitionDto>>.Fail(400, "Invalid filter", "Choose a valid shift.");
            }

            requisitions = requisitions.Where(item => item.ShiftType == shift);
        }

        if (!string.IsNullOrWhiteSpace(query.EmploymentType))
        {
            if (!Enum.TryParse<EmploymentType>(query.EmploymentType, true, out var employment))
            {
                return OperationResult<PageDto<RequisitionDto>>.Fail(400, "Invalid filter", "Choose a valid employment type.");
            }

            requisitions = requisitions.Where(item => item.EmploymentType == employment);
        }

        if (query.AssignedToMe == true)
        {
            requisitions = requisitions.Where(item => item.AssignedRecruiterId == userId);
        }

        requisitions = query.Sort?.ToLowerInvariant() switch
        {
            "title" => requisitions.OrderBy(item => item.JobTitle),
            "-title" => requisitions.OrderByDescending(item => item.JobTitle),
            "specialty" => requisitions.OrderBy(item => item.Specialty),
            "status" => requisitions.OrderBy(item => item.Status),
            _ => requisitions.OrderByDescending(item => item.CreatedAt)
        };

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 50 ? 10 : query.PageSize;
        var total = await requisitions.CountAsync(cancellationToken);
        var items = await requisitions.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return OperationResult<PageDto<RequisitionDto>>.Ok(new PageDto<RequisitionDto>(items.Select(Map).ToArray(), page, pageSize, total));
    }

    public async Task<OperationResult<RequisitionDto>> GetAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        var requisition = await Visible(await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken), userId)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (requisition is null)
        {
            var exists = await _db.JobRequisitions.AnyAsync(item => item.Id == id, cancellationToken);
            return exists
                ? OperationResult<RequisitionDto>.Fail(403, "Forbidden", "You cannot view this requisition.")
                : OperationResult<RequisitionDto>.Fail(404, "Not found", "That requisition does not exist.");
        }

        return OperationResult<RequisitionDto>.Ok(Map(requisition));
    }

    public async Task<OperationResult<RequisitionDto>> UpdateAsync(Guid id, RequisitionRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var requisition = await _db.JobRequisitions.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (requisition is null)
        {
            return OperationResult<RequisitionDto>.Fail(404, "Not found", "That requisition does not exist.");
        }

        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        var canManage = access.IsAdministrator || access.ManagedFacilityIds.Contains(requisition.FacilityId);
        var canRecruit = access.IsRecruiter && (requisition.AssignedRecruiterId == userId || access.RecruiterFacilityIds.Contains(requisition.FacilityId));
        if (!canManage && !canRecruit)
        {
            return OperationResult<RequisitionDto>.Fail(403, "Forbidden", "You cannot update this requisition.");
        }

        if (requisition.Status is RequisitionStatus.Closed or RequisitionStatus.Cancelled)
        {
            return OperationResult<RequisitionDto>.Fail(409, "Closed requisition", "A closed requisition cannot be edited.");
        }

        if (canManage)
        {
            var location = await _db.FacilityLocations.FirstOrDefaultAsync(
                item => item.Id == request.LocationId && item.FacilityId == requisition.FacilityId && item.IsActive,
                cancellationToken);
            if (location is null || request.FacilityId != requisition.FacilityId)
            {
                return OperationResult<RequisitionDto>.Fail(400, "Invalid location", "Choose an active location that belongs to the facility.");
            }

            requisition.LocationId = request.LocationId;
            requisition.JobTitle = request.JobTitle.Trim();
            requisition.OpenPositions = request.OpenPositions;
            requisition.PayRateMin = request.PayRateMin;
            requisition.PayRateMax = request.PayRateMax;
            requisition.StartDate = request.StartDate;
            requisition.EndDate = request.EndDate;
            requisition.EmploymentType = Enum.Parse<EmploymentType>(request.EmploymentType, true);
            requisition.ShiftType = Enum.Parse<ShiftType>(request.ShiftType, true);
        }

        requisition.Description = request.Description.Trim();
        requisition.Specialty = request.Specialty.Trim();
        requisition.RequiredExperience = request.RequiredExperience;
        requisition.RequiredCertifications = request.RequiredCertifications.Trim();
        requisition.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("RequisitionUpdated", userId, true, requisition.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<RequisitionDto>.Ok(await MapAsync(requisition.Id, cancellationToken));
    }

    public async Task<OperationResult<RequisitionDto>> ChangeStatusAsync(Guid id, RequisitionStatusRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var requisition = await _db.JobRequisitions.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (requisition is null)
        {
            return OperationResult<RequisitionDto>.Fail(404, "Not found", "That requisition does not exist.");
        }

        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        var canManage = access.IsAdministrator || access.ManagedFacilityIds.Contains(requisition.FacilityId);
        var canRecruit = access.IsRecruiter && requisition.AssignedRecruiterId == userId;
        if (!canManage && !canRecruit)
        {
            return OperationResult<RequisitionDto>.Fail(403, "Forbidden", "You cannot change this requisition status.");
        }

        var next = Enum.Parse<RequisitionStatus>(request.Status, true);
        if (next == requisition.Status)
        {
            return OperationResult<RequisitionDto>.Ok(await MapAsync(requisition.Id, cancellationToken));
        }

        if (!Transitions[requisition.Status].Contains(next))
        {
            return OperationResult<RequisitionDto>.Fail(409, "Invalid status", $"A requisition cannot move from {requisition.Status} to {next}.");
        }

        requisition.Status = next;
        requisition.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("RequisitionStatusChanged", userId, true, $"{requisition.Id}:{next}", ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<RequisitionDto>.Ok(await MapAsync(requisition.Id, cancellationToken));
    }

    public async Task<OperationResult<RequisitionDto>> AssignRecruiterAsync(Guid id, AssignRecruiterRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        if (!await StaffingAccess.IsAdministratorAsync(_users, userId))
        {
            return OperationResult<RequisitionDto>.Fail(403, "Forbidden", "Only an administrator can assign a recruiter.");
        }

        var requisition = await _db.JobRequisitions.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (requisition is null)
        {
            return OperationResult<RequisitionDto>.Fail(404, "Not found", "That requisition does not exist.");
        }

        var recruiter = await _users.FindByIdAsync(request.RecruiterId) ?? await _users.FindByEmailAsync(request.RecruiterId.Trim());
        if (recruiter is null || !recruiter.IsActive)
        {
            return OperationResult<RequisitionDto>.Fail(404, "Not found", "That recruiter account is not available.");
        }

        var roles = await _users.GetRolesAsync(recruiter);
        if (!roles.Any(name => AppRoles.RecruiterAccess.Contains(name, StringComparer.Ordinal) && name != AppRoles.Administrator))
        {
            return OperationResult<RequisitionDto>.Fail(409, "Role mismatch", "The selected account is not a recruiter.");
        }

        requisition.AssignedRecruiterId = recruiter.Id;
        requisition.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("RecruiterAssigned", userId, true, requisition.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<RequisitionDto>.Ok(await MapAsync(requisition.Id, cancellationToken));
    }

    private IQueryable<JobRequisition> Visible(StaffingAccess.Actor access, string userId)
    {
        var query = _db.JobRequisitions.AsNoTracking().Include(item => item.Facility).Include(item => item.Location).AsQueryable();
        if (access.IsAdministrator)
        {
            return query;
        }

        if (access.IsClinicianOnly)
        {
            return query.Where(item => item.Status == RequisitionStatus.Published);
        }

        return query.Where(item =>
            access.ManagedFacilityIds.Contains(item.FacilityId)
            || (access.IsRecruiter && (item.AssignedRecruiterId == userId || access.RecruiterFacilityIds.Contains(item.FacilityId))));
    }

    private async Task<RequisitionDto> MapAsync(Guid id, CancellationToken cancellationToken)
    {
        var requisition = await _db.JobRequisitions.AsNoTracking()
            .Include(item => item.Facility)
            .Include(item => item.Location)
            .FirstAsync(item => item.Id == id, cancellationToken);
        return Map(requisition);
    }

    private static RequisitionDto Map(JobRequisition requisition) => new(
        requisition.Id,
        requisition.FacilityId,
        requisition.Facility.Name,
        requisition.LocationId,
        requisition.Location.LocationName,
        requisition.Location.City,
        requisition.Location.State,
        requisition.JobTitle,
        requisition.Description,
        requisition.Specialty,
        requisition.EmploymentType.ToString(),
        requisition.ShiftType.ToString(),
        requisition.RequiredExperience,
        requisition.RequiredCertifications,
        requisition.OpenPositions,
        requisition.PayRateMin,
        requisition.PayRateMax,
        requisition.StartDate,
        requisition.EndDate,
        requisition.Status.ToString(),
        requisition.CreatedBy,
        requisition.AssignedRecruiterId,
        requisition.CreatedAt,
        requisition.UpdatedAt);
}

internal static class StaffingAccess
{
    public sealed record Actor(
        bool IsAdministrator,
        bool IsFacilityManager,
        bool IsRecruiter,
        bool IsClinicianOnly,
        Guid[] ManagedFacilityIds,
        Guid[] RecruiterFacilityIds);

    public static async Task<bool> IsAdministratorAsync(UserManager<ApplicationUser> users, string userId)
    {
        var user = await users.FindByIdAsync(userId);
        return user is not null && await users.IsInRoleAsync(user, AppRoles.Administrator);
    }

    public static async Task<Actor> DescribeAsync(
        UserManager<ApplicationUser> users,
        ApplicationDbContext db,
        string userId,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId);
        var roles = user is null ? [] : await users.GetRolesAsync(user);
        var admin = roles.Contains(AppRoles.Administrator, StringComparer.Ordinal);
        var facility = roles.Any(role => AppRoles.FacilityManagerAccess.Contains(role, StringComparer.Ordinal));
        var recruiter = roles.Any(role => AppRoles.RecruiterAccess.Contains(role, StringComparer.Ordinal));
        var memberships = await db.FacilityMemberships.AsNoTracking()
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken);
        return new Actor(
            admin,
            facility,
            recruiter,
            !admin && !facility && !recruiter,
            memberships.Where(item => item.MembershipRole == MembershipRole.Manager).Select(item => item.FacilityId).ToArray(),
            memberships.Where(item => item.MembershipRole == MembershipRole.Recruiter).Select(item => item.FacilityId).ToArray());
    }

    public static async Task<bool> CanViewFacilityAsync(
        UserManager<ApplicationUser> users,
        ApplicationDbContext db,
        string userId,
        Guid facilityId,
        CancellationToken cancellationToken)
    {
        var access = await DescribeAsync(users, db, userId, cancellationToken);
        if (access.IsClinicianOnly)
        {
            return false;
        }

        return access.IsAdministrator
            || access.ManagedFacilityIds.Contains(facilityId)
            || access.RecruiterFacilityIds.Contains(facilityId)
            || await db.JobRequisitions.AnyAsync(item => item.FacilityId == facilityId && item.AssignedRecruiterId == userId, cancellationToken);
    }

    public static async Task<bool> CanManageFacilityAsync(
        UserManager<ApplicationUser> users,
        ApplicationDbContext db,
        string userId,
        Guid facilityId,
        CancellationToken cancellationToken)
    {
        var access = await DescribeAsync(users, db, userId, cancellationToken);
        return access.IsAdministrator || access.ManagedFacilityIds.Contains(facilityId);
    }
}
