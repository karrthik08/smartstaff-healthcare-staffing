using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartStaff.Application.Authentication;
using SmartStaff.Application.Common;
using SmartStaff.Application.Scheduling;
using SmartStaff.Application.Staffing;
using SmartStaff.Domain.Recruitment;
using SmartStaff.Domain.Scheduling;
using SmartStaff.Domain.Staffing;
using SmartStaff.Infrastructure.Identity;
using SmartStaff.Infrastructure.Persistence;
using SmartStaff.Infrastructure.Staffing;

namespace SmartStaff.Infrastructure.Scheduling;

public sealed class SchedulingService : ISchedulingService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditWriter _audit;

    public SchedulingService(ApplicationDbContext db, UserManager<ApplicationUser> users, IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    public async Task<OperationResult<ShiftDto>> CreateAsync(ShiftRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        if (!await CanManageFacilityAsync(request.FacilityId, userId, cancellationToken))
        {
            return OperationResult<ShiftDto>.Fail(403, "Forbidden", "You cannot create shifts for this facility.");
        }

        var converted = await ConvertShiftAsync(request, cancellationToken);
        if (converted.Error is not null)
        {
            return OperationResult<ShiftDto>.Fail(converted.Status, converted.Error, converted.Detail);
        }

        var shift = new Shift
        {
            FacilityId = request.FacilityId,
            FacilityLocationId = request.FacilityLocationId,
            JobRequisitionId = request.JobRequisitionId,
            StartDateTime = converted.Start,
            EndDateTime = converted.End,
            RequiredClinicians = request.RequiredClinicians,
            Specialty = request.Specialty.Trim(),
            CreatedBy = userId
        };
        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ShiftCreated", userId, true, shift.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ShiftDto>.Ok(await MapShiftAsync(shift.Id, cancellationToken));
    }

    public async Task<OperationResult<ShiftDto>> UpdateAsync(Guid id, ShiftRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var shift = await _db.Shifts.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (shift is null)
        {
            return OperationResult<ShiftDto>.Fail(404, "Not found", "That shift does not exist.");
        }

        if (!await CanManageFacilityAsync(shift.FacilityId, userId, cancellationToken) || request.FacilityId != shift.FacilityId)
        {
            return OperationResult<ShiftDto>.Fail(403, "Forbidden", "You cannot update this shift.");
        }

        if (shift.Status == ShiftScheduleStatus.Cancelled)
        {
            return OperationResult<ShiftDto>.Fail(409, "Cancelled shift", "A cancelled shift cannot be edited.");
        }

        var converted = await ConvertShiftAsync(request, cancellationToken);
        if (converted.Error is not null)
        {
            return OperationResult<ShiftDto>.Fail(converted.Status, converted.Error, converted.Detail);
        }

        shift.FacilityLocationId = request.FacilityLocationId;
        shift.JobRequisitionId = request.JobRequisitionId;
        shift.StartDateTime = converted.Start;
        shift.EndDateTime = converted.End;
        shift.RequiredClinicians = request.RequiredClinicians;
        shift.Specialty = request.Specialty.Trim();
        shift.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ShiftUpdated", userId, true, shift.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ShiftDto>.Ok(await MapShiftAsync(shift.Id, cancellationToken));
    }

    public async Task<OperationResult<ShiftDto>> PublishAsync(Guid id, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var shift = await _db.Shifts.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (shift is null)
        {
            return OperationResult<ShiftDto>.Fail(404, "Not found", "That shift does not exist.");
        }

        if (!await CanManageFacilityAsync(shift.FacilityId, userId, cancellationToken))
        {
            return OperationResult<ShiftDto>.Fail(403, "Forbidden", "You cannot publish this shift.");
        }

        if (shift.Status == ShiftScheduleStatus.Published)
        {
            return OperationResult<ShiftDto>.Ok(await MapShiftAsync(shift.Id, cancellationToken));
        }

        if (shift.Status != ShiftScheduleStatus.Draft)
        {
            return OperationResult<ShiftDto>.Fail(409, "Invalid status", "Only a draft shift can be published.");
        }

        shift.Status = ShiftScheduleStatus.Published;
        shift.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ShiftPublished", userId, true, shift.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ShiftDto>.Ok(await MapShiftAsync(shift.Id, cancellationToken));
    }

    public async Task<OperationResult<ShiftDto>> CancelAsync(Guid id, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var shift = await _db.Shifts.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (shift is null)
        {
            return OperationResult<ShiftDto>.Fail(404, "Not found", "That shift does not exist.");
        }

        if (!await CanManageFacilityAsync(shift.FacilityId, userId, cancellationToken))
        {
            return OperationResult<ShiftDto>.Fail(403, "Forbidden", "You cannot cancel this shift.");
        }

        if (shift.Status == ShiftScheduleStatus.Cancelled)
        {
            return OperationResult<ShiftDto>.Ok(await MapShiftAsync(shift.Id, cancellationToken));
        }

        var now = DateTimeOffset.UtcNow;
        shift.Status = ShiftScheduleStatus.Cancelled;
        shift.UpdatedAt = now;
        var assignments = await _db.ShiftAssignments.Where(item => item.ShiftId == id && item.AssignmentStatus != AssignmentStatus.Cancelled).ToListAsync(cancellationToken);
        foreach (var assignment in assignments)
        {
            assignment.AssignmentStatus = AssignmentStatus.Cancelled;
            assignment.CancelledAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ShiftCancelled", userId, true, shift.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ShiftDto>.Ok(await MapShiftAsync(shift.Id, cancellationToken));
    }

    public async Task<OperationResult<PageDto<ShiftDto>>> ListAsync(ShiftListQuery query, string userId, CancellationToken cancellationToken)
    {
        var visible = await VisibleShiftIdsAsync(userId, cancellationToken);
        var shifts = _db.Shifts.AsNoTracking().Where(item => visible.Contains(item.Id));
        if (query.FacilityId is Guid facilityId)
        {
            shifts = shifts.Where(item => item.FacilityId == facilityId);
        }

        if (query.From is DateTimeOffset from)
        {
            shifts = shifts.Where(item => item.EndDateTime > from);
        }

        if (query.To is DateTimeOffset to)
        {
            shifts = shifts.Where(item => item.StartDateTime < to);
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<ShiftScheduleStatus>(query.Status, true, out var status))
        {
            shifts = shifts.Where(item => item.Status == status);
        }

        if (string.Equals(query.View, "open", StringComparison.OrdinalIgnoreCase))
        {
            var filled = _db.ShiftAssignments.Where(item => item.AssignmentStatus != AssignmentStatus.Cancelled)
                .GroupBy(item => item.ShiftId)
                .Select(group => new { ShiftId = group.Key, Count = group.Count() });
            shifts = shifts.Where(item => item.Status != ShiftScheduleStatus.Cancelled && !filled.Any(row => row.ShiftId == item.Id && row.Count >= item.RequiredClinicians));
        }
        else if (string.Equals(query.View, "filled", StringComparison.OrdinalIgnoreCase))
        {
            var filled = _db.ShiftAssignments.Where(item => item.AssignmentStatus != AssignmentStatus.Cancelled)
                .GroupBy(item => item.ShiftId)
                .Select(group => new { ShiftId = group.Key, Count = group.Count() });
            shifts = shifts.Where(item => filled.Any(row => row.ShiftId == item.Id && row.Count >= item.RequiredClinicians));
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 50 ? 20 : query.PageSize;
        var total = await shifts.CountAsync(cancellationToken);
        var ids = await shifts.OrderBy(item => item.StartDateTime).Skip((page - 1) * pageSize).Take(pageSize).Select(item => item.Id).ToListAsync(cancellationToken);
        var items = new List<ShiftDto>();
        foreach (var id in ids)
        {
            items.Add(await MapShiftAsync(id, cancellationToken));
        }

        return OperationResult<PageDto<ShiftDto>>.Ok(new PageDto<ShiftDto>(items, page, pageSize, total));
    }

    public async Task<OperationResult<ShiftDto>> GetAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        if (!await VisibleContainsAsync(userId, id, cancellationToken))
        {
            var exists = await _db.Shifts.AnyAsync(item => item.Id == id, cancellationToken);
            return exists
                ? OperationResult<ShiftDto>.Fail(403, "Forbidden", "You cannot view this shift.")
                : OperationResult<ShiftDto>.Fail(404, "Not found", "That shift does not exist.");
        }

        return OperationResult<ShiftDto>.Ok(await MapShiftAsync(id, cancellationToken));
    }

    public async Task<OperationResult<AssignmentDto>> AssignAsync(Guid shiftId, AssignShiftRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var shift = await _db.Shifts.AsNoTracking().FirstOrDefaultAsync(item => item.Id == shiftId, cancellationToken);
        if (shift is null)
        {
            return OperationResult<AssignmentDto>.Fail(404, "Not found", "That shift does not exist.");
        }

        if (!await CanAssignAsync(shift, userId, cancellationToken))
        {
            return OperationResult<AssignmentDto>.Fail(403, "Forbidden", "You cannot assign clinicians to this shift.");
        }

        if (shift.Status == ShiftScheduleStatus.Cancelled)
        {
            return OperationResult<AssignmentDto>.Fail(409, "Cancelled shift", "Clinicians cannot be assigned to a cancelled shift.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await LockAsync(shift.Id, cancellationToken);
        await LockAsync(request.ClinicianProfileId, cancellationToken);
        shift = await _db.Shifts.FirstAsync(item => item.Id == shiftId, cancellationToken);
        var active = await _db.ShiftAssignments.Where(item => item.ShiftId == shift.Id && item.AssignmentStatus != AssignmentStatus.Cancelled).ToListAsync(cancellationToken);
        if (active.Any(item => item.ClinicianProfileId == request.ClinicianProfileId))
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult<AssignmentDto>.Fail(409, "Already assigned", "This clinician is already assigned to the shift.");
        }

        if (active.Count >= shift.RequiredClinicians)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult<AssignmentDto>.Fail(409, "Shift full", "This shift has no remaining capacity.");
        }

        var overlap = await (
            from current in _db.ShiftAssignments
            join other in _db.Shifts on current.ShiftId equals other.Id
            where current.ClinicianProfileId == request.ClinicianProfileId
                && current.AssignmentStatus != AssignmentStatus.Cancelled
                && other.Status != ShiftScheduleStatus.Cancelled
                && other.StartDateTime < shift.EndDateTime
                && other.EndDateTime > shift.StartDateTime
            select current.Id).AnyAsync(cancellationToken);
        if (overlap)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult<AssignmentDto>.Fail(409, "Schedule conflict", "This clinician already has an overlapping shift.");
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById((await _db.HealthcareFacilities.Where(item => item.Id == shift.FacilityId).Select(item => item.TimeZoneId).FirstAsync(cancellationToken)));
        var startDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(shift.StartDateTime, zone).DateTime);
        var endDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(shift.EndDateTime, zone).DateTime);
        var placement = await _db.Placements.FirstOrDefaultAsync(item =>
            item.ClinicianProfileId == request.ClinicianProfileId
            && item.FacilityId == shift.FacilityId
            && item.Status == PlacementStatus.Active
            && item.StartDate <= startDate
            && (item.EndDate == null || item.EndDate >= endDate), cancellationToken);
        if (placement is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult<AssignmentDto>.Fail(409, "Not eligible", "The clinician needs an active placement that covers this shift.");
        }

        var assignment = new ShiftAssignment
        {
            ShiftId = shift.Id,
            ClinicianProfileId = request.ClinicianProfileId,
            PlacementId = placement.Id,
            AssignedBy = userId
        };
        _db.ShiftAssignments.Add(assignment);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ShiftAssigned", userId, true, assignment.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult<AssignmentDto>.Ok(await MapAssignmentAsync(assignment.Id, cancellationToken));
    }

    public async Task<OperationResult<AssignmentDto>> ConfirmAsync(Guid assignmentId, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var assignment = await _db.ShiftAssignments.FirstOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
        if (assignment is null)
        {
            return OperationResult<AssignmentDto>.Fail(404, "Not found", "That assignment does not exist.");
        }

        var profile = await _db.ClinicianProfiles.AsNoTracking().FirstAsync(item => item.Id == assignment.ClinicianProfileId, cancellationToken);
        var admin = await StaffingAccess.IsAdministratorAsync(_users, userId);
        if (profile.UserId != userId && !admin)
        {
            return OperationResult<AssignmentDto>.Fail(403, "Forbidden", "Only the assigned clinician can confirm this shift.");
        }

        if (assignment.AssignmentStatus == AssignmentStatus.Confirmed)
        {
            return OperationResult<AssignmentDto>.Ok(await MapAssignmentAsync(assignment.Id, cancellationToken));
        }

        if (assignment.AssignmentStatus != AssignmentStatus.Assigned)
        {
            return OperationResult<AssignmentDto>.Fail(409, "Unavailable", "This assignment cannot be confirmed.");
        }

        assignment.AssignmentStatus = AssignmentStatus.Confirmed;
        assignment.ConfirmedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ShiftConfirmed", userId, true, assignment.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<AssignmentDto>.Ok(await MapAssignmentAsync(assignment.Id, cancellationToken));
    }

    public async Task<OperationResult<AssignmentDto>> CancelAssignmentAsync(Guid assignmentId, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var assignment = await _db.ShiftAssignments.FirstOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
        if (assignment is null)
        {
            return OperationResult<AssignmentDto>.Fail(404, "Not found", "That assignment does not exist.");
        }

        var shift = await _db.Shifts.AsNoTracking().FirstAsync(item => item.Id == assignment.ShiftId, cancellationToken);
        if (!await CanAssignAsync(shift, userId, cancellationToken))
        {
            return OperationResult<AssignmentDto>.Fail(403, "Forbidden", "You cannot cancel this assignment.");
        }

        if (assignment.AssignmentStatus != AssignmentStatus.Cancelled)
        {
            assignment.AssignmentStatus = AssignmentStatus.Cancelled;
            assignment.CancelledAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("ShiftAssignmentCancelled", userId, true, assignment.Id.ToString(), ipAddress, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return OperationResult<AssignmentDto>.Ok(await MapAssignmentAsync(assignment.Id, cancellationToken));
    }

    public async Task<OperationResult<AvailabilityDto>> AddAvailabilityAsync(AvailabilityRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var profile = await _db.ClinicianProfiles.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (profile is null)
        {
            return OperationResult<AvailabilityDto>.Fail(403, "Forbidden", "Only a clinician can record availability.");
        }

        var entry = new ClinicianAvailability
        {
            ClinicianProfileId = profile.Id,
            AvailableFrom = request.AvailableFrom,
            AvailableUntil = request.AvailableUntil,
            AvailabilityType = Enum.Parse<AvailabilityType>(request.AvailabilityType, true),
            Notes = request.Notes?.Trim() ?? ""
        };
        _db.ClinicianAvailability.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("AvailabilityRecorded", userId, true, entry.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<AvailabilityDto>.Ok(MapAvailability(entry));
    }

    public async Task<OperationResult<IReadOnlyList<AvailabilityDto>>> ListAvailabilityAsync(string userId, CancellationToken cancellationToken)
    {
        var profileId = await _db.ClinicianProfiles.Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).FirstOrDefaultAsync(cancellationToken);
        if (profileId is null)
        {
            return OperationResult<IReadOnlyList<AvailabilityDto>>.Fail(403, "Forbidden", "Only a clinician can view this availability.");
        }

        var items = await _db.ClinicianAvailability.AsNoTracking().Where(item => item.ClinicianProfileId == profileId).OrderBy(item => item.AvailableFrom).ToListAsync(cancellationToken);
        return OperationResult<IReadOnlyList<AvailabilityDto>>.Ok(items.Select(MapAvailability).ToArray());
    }

    public async Task<OperationResult<ShiftChangeDto>> RequestChangeAsync(ShiftChangeRequestBody request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var assignment = await _db.ShiftAssignments.FirstOrDefaultAsync(item => item.Id == request.ShiftAssignmentId, cancellationToken);
        if (assignment is null)
        {
            return OperationResult<ShiftChangeDto>.Fail(404, "Not found", "That assignment does not exist.");
        }

        var profile = await _db.ClinicianProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == assignment.ClinicianProfileId, cancellationToken);
        if (profile is null || profile.UserId != userId)
        {
            return OperationResult<ShiftChangeDto>.Fail(403, "Forbidden", "You can request changes only for your own assignments.");
        }

        if (assignment.AssignmentStatus == AssignmentStatus.Cancelled)
        {
            return OperationResult<ShiftChangeDto>.Fail(409, "Cancelled assignment", "This assignment is already cancelled.");
        }

        if (await _db.ShiftChangeRequests.AnyAsync(item => item.ShiftAssignmentId == assignment.Id && item.Status == ShiftChangeStatus.Pending, cancellationToken))
        {
            return OperationResult<ShiftChangeDto>.Fail(409, "Request pending", "A change request is already waiting for review.");
        }

        var change = new ShiftChangeRequest
        {
            ShiftAssignmentId = assignment.Id,
            RequestedBy = userId,
            RequestType = Enum.Parse<ShiftChangeType>(request.RequestType, true),
            Reason = request.Reason.Trim()
        };
        _db.ShiftChangeRequests.Add(change);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ShiftChangeRequested", userId, true, change.Id.ToString(), ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ShiftChangeDto>.Ok(await MapChangeAsync(change.Id, cancellationToken));
    }

    public async Task<OperationResult<IReadOnlyList<ShiftChangeDto>>> ListChangesAsync(string userId, CancellationToken cancellationToken)
    {
        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        var rows = await (
            from change in _db.ShiftChangeRequests.AsNoTracking()
            join assignment in _db.ShiftAssignments.AsNoTracking() on change.ShiftAssignmentId equals assignment.Id
            join shift in _db.Shifts.AsNoTracking() on assignment.ShiftId equals shift.Id
            join requisition in _db.JobRequisitions.AsNoTracking() on shift.JobRequisitionId equals requisition.Id
            select new { change, assignment, shift, requisition }).ToListAsync(cancellationToken);
        var visible = rows.Where(row =>
                access.IsAdministrator
                || row.change.RequestedBy == userId
                || access.ManagedFacilityIds.Contains(row.shift.FacilityId)
                || (access.IsRecruiter && row.requisition.AssignedRecruiterId == userId))
            .Select(row => MapChange(row.change, row.shift.Id))
            .ToArray();
        return OperationResult<IReadOnlyList<ShiftChangeDto>>.Ok(visible);
    }

    public async Task<OperationResult<ShiftChangeDto>> ReviewChangeAsync(Guid id, ReviewShiftChangeRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var change = await _db.ShiftChangeRequests.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (change is null)
        {
            return OperationResult<ShiftChangeDto>.Fail(404, "Not found", "That change request does not exist.");
        }

        var assignment = await _db.ShiftAssignments.FirstAsync(item => item.Id == change.ShiftAssignmentId, cancellationToken);
        var shift = await _db.Shifts.AsNoTracking().FirstAsync(item => item.Id == assignment.ShiftId, cancellationToken);
        if (!await CanAssignAsync(shift, userId, cancellationToken))
        {
            return OperationResult<ShiftChangeDto>.Fail(403, "Forbidden", "You cannot review this change request.");
        }

        if (change.Status != ShiftChangeStatus.Pending)
        {
            return OperationResult<ShiftChangeDto>.Ok(await MapChangeAsync(change.Id, cancellationToken));
        }

        var decision = Enum.Parse<ShiftChangeStatus>(request.Status, true);
        change.Status = decision;
        change.ReviewedBy = userId;
        change.ReviewedAt = DateTimeOffset.UtcNow;
        if (decision == ShiftChangeStatus.Approved && change.RequestType == ShiftChangeType.Cancel && assignment.AssignmentStatus != AssignmentStatus.Cancelled)
        {
            assignment.AssignmentStatus = AssignmentStatus.Cancelled;
            assignment.CancelledAt = change.ReviewedAt;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ShiftChangeReviewed", userId, true, $"{change.Id}:{decision}", ipAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ShiftChangeDto>.Ok(await MapChangeAsync(change.Id, cancellationToken));
    }

    private async Task<ConvertedShift> ConvertShiftAsync(ShiftRequest request, CancellationToken cancellationToken)
    {
        var facility = await _db.HealthcareFacilities.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.FacilityId, cancellationToken);
        if (facility is null)
        {
            return ConvertedShift.Fail(404, "Not found", "That facility does not exist.");
        }

        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(facility.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return ConvertedShift.Fail(409, "Invalid timezone", "The facility timezone is not recognized.");
        }

        if (zone.IsInvalidTime(request.StartLocal) || zone.IsInvalidTime(request.EndLocal))
        {
            return ConvertedShift.Fail(400, "Invalid local time", "That local time does not exist because of a daylight-saving transition.");
        }

        if (zone.IsAmbiguousTime(request.StartLocal) || zone.IsAmbiguousTime(request.EndLocal))
        {
            return ConvertedShift.Fail(400, "Ambiguous local time", "That local time occurs twice because of a daylight-saving transition.");
        }

        var locationOk = await _db.FacilityLocations.AnyAsync(item => item.Id == request.FacilityLocationId && item.FacilityId == facility.Id && item.IsActive, cancellationToken);
        var requisitionOk = await _db.JobRequisitions.AnyAsync(item => item.Id == request.JobRequisitionId && item.FacilityId == facility.Id && item.LocationId == request.FacilityLocationId, cancellationToken);
        if (!locationOk || !requisitionOk)
        {
            return ConvertedShift.Fail(400, "Invalid shift", "Choose a location and requisition that belong to the facility.");
        }

        var start = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(request.StartLocal, DateTimeKind.Unspecified), zone), TimeSpan.Zero);
        var end = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(request.EndLocal, DateTimeKind.Unspecified), zone), TimeSpan.Zero);
        return end <= start
            ? ConvertedShift.Fail(400, "Invalid shift", "The shift end must be after the start.")
            : new ConvertedShift(start, end, null, 0, "");
    }

    private async Task<bool> CanManageFacilityAsync(Guid facilityId, string userId, CancellationToken cancellationToken) =>
        await StaffingAccess.CanManageFacilityAsync(_users, _db, userId, facilityId, cancellationToken);

    private async Task<bool> CanAssignAsync(Shift shift, string userId, CancellationToken cancellationToken)
    {
        if (await CanManageFacilityAsync(shift.FacilityId, userId, cancellationToken))
        {
            return true;
        }

        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        if (!access.IsRecruiter)
        {
            return false;
        }

        return await _db.JobRequisitions.AnyAsync(item => item.Id == shift.JobRequisitionId && item.AssignedRecruiterId == userId, cancellationToken);
    }

    private async Task<List<Guid>> VisibleShiftIdsAsync(string userId, CancellationToken cancellationToken)
    {
        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        var query = from shift in _db.Shifts.AsNoTracking()
                    join requisition in _db.JobRequisitions.AsNoTracking() on shift.JobRequisitionId equals requisition.Id
                    select new { shift.Id, shift.FacilityId, requisition.AssignedRecruiterId };
        if (access.IsAdministrator)
        {
            return await query.Select(item => item.Id).ToListAsync(cancellationToken);
        }

        var profileId = await _db.ClinicianProfiles.Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).FirstOrDefaultAsync(cancellationToken);
        var mine = _db.ShiftAssignments.Where(item => item.ClinicianProfileId == profileId).Select(item => item.ShiftId);
        return await query.Where(item =>
                mine.Contains(item.Id)
                || access.ManagedFacilityIds.Contains(item.FacilityId)
                || (access.IsRecruiter && item.AssignedRecruiterId == userId))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> VisibleContainsAsync(string userId, Guid id, CancellationToken cancellationToken) =>
        (await VisibleShiftIdsAsync(userId, cancellationToken)).Contains(id);

    private async Task LockAsync(Guid id, CancellationToken cancellationToken)
    {
        var key = BitConverter.ToInt64(id.ToByteArray(), 0);
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }

    private async Task<ShiftDto> MapShiftAsync(Guid id, CancellationToken cancellationToken)
    {
        var shift = await _db.Shifts.AsNoTracking().FirstAsync(item => item.Id == id, cancellationToken);
        var facility = await _db.HealthcareFacilities.AsNoTracking().FirstAsync(item => item.Id == shift.FacilityId, cancellationToken);
        var location = await _db.FacilityLocations.AsNoTracking().FirstAsync(item => item.Id == shift.FacilityLocationId, cancellationToken);
        var assigned = await _db.ShiftAssignments.CountAsync(item => item.ShiftId == id && item.AssignmentStatus != AssignmentStatus.Cancelled, cancellationToken);
        var confirmed = await _db.ShiftAssignments.CountAsync(item => item.ShiftId == id && item.AssignmentStatus == AssignmentStatus.Confirmed, cancellationToken);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(facility.TimeZoneId);
        return new ShiftDto(
            shift.Id,
            facility.Id,
            facility.Name,
            facility.TimeZoneId,
            location.Id,
            location.LocationName,
            shift.JobRequisitionId,
            shift.StartDateTime,
            shift.EndDateTime,
            TimeZoneInfo.ConvertTime(shift.StartDateTime, zone).ToString("yyyy-MM-ddTHH:mm"),
            TimeZoneInfo.ConvertTime(shift.EndDateTime, zone).ToString("yyyy-MM-ddTHH:mm"),
            shift.RequiredClinicians,
            assigned,
            confirmed,
            shift.Specialty,
            shift.Status.ToString());
    }

    private async Task<AssignmentDto> MapAssignmentAsync(Guid id, CancellationToken cancellationToken)
    {
        var assignment = await _db.ShiftAssignments.AsNoTracking().FirstAsync(item => item.Id == id, cancellationToken);
        var profile = await _db.ClinicianProfiles.AsNoTracking().FirstAsync(item => item.Id == assignment.ClinicianProfileId, cancellationToken);
        return new AssignmentDto(
            assignment.Id,
            assignment.ShiftId,
            profile.Id,
            $"{profile.FirstName} {profile.LastName}",
            assignment.PlacementId,
            assignment.AssignmentStatus.ToString(),
            assignment.AssignedAt,
            assignment.ConfirmedAt,
            assignment.CancelledAt);
    }

    private static AvailabilityDto MapAvailability(ClinicianAvailability entry) => new(
        entry.Id,
        entry.ClinicianProfileId,
        entry.AvailableFrom,
        entry.AvailableUntil,
        entry.AvailabilityType.ToString(),
        entry.Notes);

    private async Task<ShiftChangeDto> MapChangeAsync(Guid id, CancellationToken cancellationToken)
    {
        var change = await _db.ShiftChangeRequests.AsNoTracking().FirstAsync(item => item.Id == id, cancellationToken);
        var shiftId = await _db.ShiftAssignments.Where(item => item.Id == change.ShiftAssignmentId).Select(item => item.ShiftId).FirstAsync(cancellationToken);
        return MapChange(change, shiftId);
    }

    private static ShiftChangeDto MapChange(ShiftChangeRequest change, Guid shiftId) => new(
        change.Id,
        change.ShiftAssignmentId,
        shiftId,
        change.RequestType.ToString(),
        change.Reason,
        change.Status.ToString(),
        change.CreatedAt,
        change.ReviewedAt);

    private sealed record ConvertedShift(DateTimeOffset Start, DateTimeOffset End, string? Error, int Status, string Detail)
    {
        public static ConvertedShift Fail(int status, string error, string detail) => new(default, default, error, status, detail);
    }
}
