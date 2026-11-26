using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartStaff.Application.Common;
using SmartStaff.Application.Timekeeping;
using SmartStaff.Domain.Recruitment;
using SmartStaff.Domain.Scheduling;
using SmartStaff.Domain.Staffing;
using SmartStaff.Domain.Timekeeping;
using SmartStaff.Infrastructure.Identity;
using SmartStaff.Infrastructure.Persistence;
using SmartStaff.Infrastructure.Staffing;

namespace SmartStaff.Infrastructure.Timekeeping;

public sealed class TimekeepingService : ITimekeepingService
{
    private const string FederalCode = "US-FED";
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditWriter _audit;

    public TimekeepingService(ApplicationDbContext db, UserManager<ApplicationUser> users, IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    public async Task<OperationResult<AttendanceDto>> ClockInAsync(Guid assignmentId, ClockRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var at = request.At ?? DateTimeOffset.UtcNow;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var gate = await LoadAssignmentAsync(assignmentId, userId, cancellationToken);
        if (gate.Error is not null)
        {
            return gate.Error;
        }

        await LockAsync(gate.ProfileId, cancellationToken);
        var existing = await _db.AttendanceRecords.FirstOrDefaultAsync(item => item.ShiftAssignmentId == assignmentId, cancellationToken);
        if (existing is not null)
        {
            if (existing.AttendanceStatus == AttendanceStatus.Open)
            {
                await transaction.CommitAsync(cancellationToken);
                return OperationResult<AttendanceDto>.Ok(await MapAttendanceAsync(existing, false, cancellationToken));
            }

            return OperationResult<AttendanceDto>.Fail(409, "Conflict", "This shift already has a completed attendance record.");
        }

        if (await OverlapsAsync(gate.ProfileId, Guid.Empty, at, null, cancellationToken))
        {
            return OperationResult<AttendanceDto>.Fail(409, "Conflict", "That clock-in overlaps another attendance record.");
        }

        var record = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            ShiftAssignmentId = assignmentId,
            ClinicianProfileId = gate.ProfileId,
            ClockInTime = at,
            AttendanceStatus = AttendanceStatus.Open,
            RecordedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.AttendanceRecords.Add(record);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ClockIn", userId, true, record.Id.ToString(), ipAddress, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult<AttendanceDto>.Ok(await MapAttendanceAsync(record, true, cancellationToken));
    }

    public async Task<OperationResult<AttendanceDto>> ClockOutAsync(Guid assignmentId, ClockRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var at = request.At ?? DateTimeOffset.UtcNow;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var gate = await LoadAssignmentAsync(assignmentId, userId, cancellationToken);
        if (gate.Error is not null)
        {
            return gate.Error;
        }

        await LockAsync(gate.ProfileId, cancellationToken);
        var record = await _db.AttendanceRecords.FirstOrDefaultAsync(item => item.ShiftAssignmentId == assignmentId, cancellationToken);
        if (record is null || record.AttendanceStatus != AttendanceStatus.Open && record.ClockOutTime is null)
        {
            return OperationResult<AttendanceDto>.Fail(409, "Conflict", "Clock out requires an open clock-in.");
        }

        if (record.AttendanceStatus == AttendanceStatus.Completed)
        {
            await transaction.CommitAsync(cancellationToken);
            return OperationResult<AttendanceDto>.Ok(await MapAttendanceAsync(record, false, cancellationToken));
        }

        if (at <= record.ClockInTime)
        {
            return OperationResult<AttendanceDto>.Fail(400, "Validation", "Clock-out must be after clock-in.");
        }

        if (await OverlapsAsync(gate.ProfileId, record.Id, record.ClockInTime, at, cancellationToken))
        {
            return OperationResult<AttendanceDto>.Fail(409, "Conflict", "That attendance window overlaps another record.");
        }

        record.ClockOutTime = at;
        record.AttendanceStatus = AttendanceStatus.Completed;
        record.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ClockOut", userId, true, record.Id.ToString(), ipAddress, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult<AttendanceDto>.Ok(await MapAttendanceAsync(record, false, cancellationToken));
    }

    public async Task<OperationResult<IReadOnlyList<AttendanceDto>>> ListAttendanceAsync(string userId, CancellationToken cancellationToken)
    {
        var visible = await VisibleAssignmentsAsync(userId, cancellationToken);
        var records = await _db.AttendanceRecords.AsNoTracking()
            .Where(item => visible.Contains(item.ShiftAssignmentId))
            .OrderByDescending(item => item.ClockInTime)
            .Take(100)
            .ToListAsync(cancellationToken);
        var mapped = new List<AttendanceDto>();
        foreach (var record in records)
        {
            mapped.Add(await MapAttendanceAsync(record, false, cancellationToken));
        }

        return OperationResult<IReadOnlyList<AttendanceDto>>.Ok(mapped);
    }

    public async Task<OperationResult<TimesheetDto>> CreateTimesheetAsync(TimesheetRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var profile = await OwnProfileAsync(userId, cancellationToken);
        if (profile is null)
        {
            return OperationResult<TimesheetDto>.Fail(403, "Forbidden", "Only the assigned clinician can create this timesheet.");
        }

        var eligible = await _db.Placements.AnyAsync(item => item.ClinicianProfileId == profile.Id && item.FacilityId == request.FacilityId && item.Status == PlacementStatus.Active, cancellationToken);
        if (!eligible)
        {
            return OperationResult<TimesheetDto>.Fail(403, "Forbidden", "A timesheet requires an active placement at that facility.");
        }

        var existing = await _db.Timesheets.FirstOrDefaultAsync(item => item.ClinicianProfileId == profile.Id && item.FacilityId == request.FacilityId && item.PeriodStart == request.PeriodStart, cancellationToken);
        if (existing is not null)
        {
            return OperationResult<TimesheetDto>.Ok(await MapTimesheetAsync(existing, false, cancellationToken));
        }

        var timesheet = new Timesheet
        {
            Id = Guid.NewGuid(),
            ClinicianProfileId = profile.Id,
            FacilityId = request.FacilityId,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            Status = TimesheetStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Timesheets.Add(timesheet);
        _db.TimesheetApprovalHistory.Add(History(timesheet.Id, TimesheetStatus.Draft, TimesheetStatus.Draft, userId, "Timesheet opened"));
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("TimesheetCreated", userId, true, timesheet.Id.ToString(), ipAddress, cancellationToken);
        return OperationResult<TimesheetDto>.Ok(await MapTimesheetAsync(timesheet, true, cancellationToken));
    }

    public async Task<OperationResult<TimesheetPageDto>> ListTimesheetsAsync(TimesheetListQuery query, string userId, CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 50 ? 20 : query.PageSize;
        var timesheets = _db.Timesheets.AsNoTracking().AsQueryable();
        timesheets = await ScopeTimesheetsAsync(timesheets, userId, cancellationToken);
        if (query.FacilityId is Guid facilityId)
        {
            timesheets = timesheets.Where(item => item.FacilityId == facilityId);
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<TimesheetStatus>(query.Status, true, out var status))
        {
            timesheets = timesheets.Where(item => item.Status == status);
        }

        var total = await timesheets.CountAsync(cancellationToken);
        var rows = await timesheets.OrderByDescending(item => item.PeriodStart).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = new List<TimesheetDto>();
        foreach (var row in rows)
        {
            items.Add(await MapTimesheetAsync(row, false, cancellationToken));
        }

        return OperationResult<TimesheetPageDto>.Ok(new TimesheetPageDto(items, page, pageSize, total));
    }

    public async Task<OperationResult<TimesheetDto>> GetTimesheetAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        var timesheet = await _db.Timesheets.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (timesheet is null)
        {
            return OperationResult<TimesheetDto>.Fail(404, "Not Found", "Timesheet not found.");
        }

        if (!await CanSeeTimesheetAsync(timesheet, userId, cancellationToken))
        {
            return OperationResult<TimesheetDto>.Fail(403, "Forbidden", "You cannot view this timesheet.");
        }

        return OperationResult<TimesheetDto>.Ok(await MapTimesheetAsync(timesheet, false, cancellationToken));
    }

    public Task<OperationResult<TimesheetDto>> AddEntryAsync(Guid timesheetId, TimesheetEntryRequest request, string userId, string? ipAddress, CancellationToken cancellationToken) =>
        SaveEntryAsync(timesheetId, null, request, userId, ipAddress, cancellationToken);

    public Task<OperationResult<TimesheetDto>> UpdateEntryAsync(Guid timesheetId, Guid entryId, TimesheetEntryRequest request, string userId, string? ipAddress, CancellationToken cancellationToken) =>
        SaveEntryAsync(timesheetId, entryId, request, userId, ipAddress, cancellationToken);

    public async Task<OperationResult<TimesheetDto>> SubmitAsync(Guid id, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await LockAsync(id, cancellationToken);
        var timesheet = await _db.Timesheets.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (timesheet is null)
        {
            return OperationResult<TimesheetDto>.Fail(404, "Not Found", "Timesheet not found.");
        }

        var profile = await OwnProfileAsync(userId, cancellationToken);
        if (profile is null || profile.Id != timesheet.ClinicianProfileId)
        {
            return OperationResult<TimesheetDto>.Fail(403, "Forbidden", "Only the owning clinician can submit this timesheet.");
        }

        if (timesheet.Status == TimesheetStatus.Submitted)
        {
            await transaction.CommitAsync(cancellationToken);
            return OperationResult<TimesheetDto>.Ok(await MapTimesheetAsync(timesheet, false, cancellationToken));
        }

        if (timesheet.Status is not (TimesheetStatus.Draft or TimesheetStatus.Rejected))
        {
            return OperationResult<TimesheetDto>.Fail(409, "Conflict", "This timesheet can no longer be submitted.");
        }

        var entries = await _db.TimesheetEntries.Where(item => item.TimesheetId == id).ToListAsync(cancellationToken);
        if (entries.Count == 0)
        {
            return OperationResult<TimesheetDto>.Fail(409, "Conflict", "Add the hours you worked before submitting.");
        }

        var calculated = await RecalculateAsync(timesheet, entries, cancellationToken);
        if (calculated is not null)
        {
            return calculated;
        }

        var previous = timesheet.Status;
        timesheet.Status = TimesheetStatus.Submitted;
        timesheet.SubmittedAt = DateTimeOffset.UtcNow;
        timesheet.RejectionReason = null;
        timesheet.UpdatedAt = DateTimeOffset.UtcNow;
        _db.TimesheetApprovalHistory.Add(History(id, previous, TimesheetStatus.Submitted, userId, "Submitted for facility review"));
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("TimesheetSubmitted", userId, true, id.ToString(), ipAddress, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult<TimesheetDto>.Ok(await MapTimesheetAsync(timesheet, false, cancellationToken));
    }

    public Task<OperationResult<TimesheetDto>> ApproveAsync(Guid id, string userId, string? ipAddress, CancellationToken cancellationToken) =>
        ReviewAsync(id, TimesheetStatus.Approved, null, userId, ipAddress, cancellationToken);

    public Task<OperationResult<TimesheetDto>> RejectAsync(Guid id, RejectTimesheetRequest request, string userId, string? ipAddress, CancellationToken cancellationToken) =>
        ReviewAsync(id, TimesheetStatus.Rejected, request.Reason.Trim(), userId, ipAddress, cancellationToken);

    public async Task<OperationResult<IReadOnlyList<ApprovalHistoryDto>>> HistoryAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        var timesheet = await GetTimesheetAsync(id, userId, cancellationToken);
        if (!timesheet.Succeeded)
        {
            return OperationResult<IReadOnlyList<ApprovalHistoryDto>>.Fail(timesheet.StatusCode, timesheet.Title, timesheet.Detail);
        }

        var rows = await _db.TimesheetApprovalHistory.AsNoTracking()
            .Where(item => item.TimesheetId == id)
            .OrderBy(item => item.ChangedAt)
            .Select(item => new ApprovalHistoryDto(item.Id, item.PreviousStatus.ToString(), item.NewStatus.ToString(), item.ChangedBy, item.ChangedAt, item.Comments))
            .ToListAsync(cancellationToken);
        return OperationResult<IReadOnlyList<ApprovalHistoryDto>>.Ok(rows);
    }

    public async Task<OperationResult<TimesheetSummaryDto>> SummaryAsync(string userId, CancellationToken cancellationToken)
    {
        var timesheets = await ScopeTimesheetsAsync(_db.Timesheets.AsNoTracking(), userId, cancellationToken);
        var draft = await timesheets.CountAsync(item => item.Status == TimesheetStatus.Draft, cancellationToken);
        var submitted = await timesheets.CountAsync(item => item.Status == TimesheetStatus.Submitted, cancellationToken);
        var approved = await timesheets.CountAsync(item => item.Status == TimesheetStatus.Approved, cancellationToken);
        var rejected = await timesheets.CountAsync(item => item.Status == TimesheetStatus.Rejected, cancellationToken);
        var assignments = await VisibleAssignmentsAsync(userId, cancellationToken);
        var open = await _db.AttendanceRecords.CountAsync(item => assignments.Contains(item.ShiftAssignmentId) && item.AttendanceStatus == AttendanceStatus.Open, cancellationToken);
        var completed = await _db.AttendanceRecords.Where(item => assignments.Contains(item.ShiftAssignmentId) && item.AttendanceStatus == AttendanceStatus.Completed).Select(item => item.ShiftAssignmentId).ToListAsync(cancellationToken);
        var entered = await _db.TimesheetEntries.Where(item => completed.Contains(item.ShiftAssignmentId)).Select(item => item.ShiftAssignmentId).ToListAsync(cancellationToken);
        return OperationResult<TimesheetSummaryDto>.Ok(new TimesheetSummaryDto(draft, submitted, approved, rejected, open, completed.Except(entered).Count()));
    }

    public async Task<OperationResult<AdjustmentDto>> RequestAdjustmentAsync(AdjustmentRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var entry = await _db.TimesheetEntries.FirstOrDefaultAsync(item => item.Id == request.TimesheetEntryId, cancellationToken);
        if (entry is null)
        {
            return OperationResult<AdjustmentDto>.Fail(404, "Not Found", "Timesheet entry not found.");
        }

        var timesheet = await _db.Timesheets.FirstAsync(item => item.Id == entry.TimesheetId, cancellationToken);
        if (timesheet.Status != TimesheetStatus.Approved)
        {
            return OperationResult<AdjustmentDto>.Fail(409, "Conflict", "Adjustments are only available for approved timesheets.");
        }

        var profile = await OwnProfileAsync(userId, cancellationToken);
        var manager = await StaffingAccess.CanManageFacilityAsync(_users, _db, userId, timesheet.FacilityId, cancellationToken);
        if ((profile is null || profile.Id != timesheet.ClinicianProfileId) && !manager)
        {
            return OperationResult<AdjustmentDto>.Fail(403, "Forbidden", "You cannot adjust this timesheet.");
        }

        if (await _db.TimesheetAdjustments.AnyAsync(item => item.TimesheetEntryId == entry.Id && item.Status == AdjustmentStatus.Pending, cancellationToken))
        {
            return OperationResult<AdjustmentDto>.Fail(409, "Conflict", "This entry already has a pending adjustment.");
        }

        var adjustment = new TimesheetAdjustment
        {
            Id = Guid.NewGuid(),
            TimesheetEntryId = entry.Id,
            RequestedBy = userId,
            OriginalHours = entry.RegularHours + entry.OvertimeHours,
            AdjustedHours = request.AdjustedHours,
            Reason = request.Reason.Trim(),
            Status = AdjustmentStatus.Pending
        };
        _db.TimesheetAdjustments.Add(adjustment);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("TimesheetAdjustmentRequested", userId, true, adjustment.Id.ToString(), ipAddress, cancellationToken);
        return OperationResult<AdjustmentDto>.Ok(MapAdjustment(adjustment));
    }

    public async Task<OperationResult<AdjustmentDto>> ReviewAdjustmentAsync(Guid id, ReviewAdjustmentRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AdjustmentStatus>(request.Status, true, out var decision) || decision == AdjustmentStatus.Pending)
        {
            return OperationResult<AdjustmentDto>.Fail(400, "Validation", "Choose approved or rejected.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var adjustment = await _db.TimesheetAdjustments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (adjustment is null)
        {
            return OperationResult<AdjustmentDto>.Fail(404, "Not Found", "Adjustment not found.");
        }

        var entry = await _db.TimesheetEntries.FirstAsync(item => item.Id == adjustment.TimesheetEntryId, cancellationToken);
        var timesheet = await _db.Timesheets.FirstAsync(item => item.Id == entry.TimesheetId, cancellationToken);
        if (!await CanReviewAsync(timesheet, userId, cancellationToken))
        {
            return OperationResult<AdjustmentDto>.Fail(timesheet.ClinicianProfileId == (await OwnProfileAsync(userId, cancellationToken))?.Id ? 403 : 403, "Forbidden", "You cannot review this adjustment.");
        }

        if (adjustment.Status != AdjustmentStatus.Pending)
        {
            await transaction.CommitAsync(cancellationToken);
            return OperationResult<AdjustmentDto>.Ok(MapAdjustment(adjustment));
        }

        adjustment.Status = decision;
        adjustment.ReviewedBy = userId;
        adjustment.ReviewedAt = DateTimeOffset.UtcNow;
        if (decision == AdjustmentStatus.Approved)
        {
            var entries = await _db.TimesheetEntries.Where(item => item.TimesheetId == timesheet.Id).ToListAsync(cancellationToken);
            var target = entries.First(item => item.Id == entry.Id);
            var policy = await PolicyForAsync(timesheet.FacilityId, cancellationToken);
            if (policy is null)
            {
                return OperationResult<AdjustmentDto>.Fail(409, "Conflict", "Configure an overtime policy for this facility's jurisdiction.");
            }

            var split = Split(adjustment.AdjustedHours, policy);
            target.RegularHours = split.Regular;
            target.OvertimeHours = split.Overtime;
            ApplyPeriodCap(entries.OrderBy(item => item.StartTime).ToList(), policy.RegularHoursPerPeriod);
            timesheet.TotalRegularHours = entries.Sum(item => item.RegularHours);
            timesheet.TotalOvertimeHours = entries.Sum(item => item.OvertimeHours);
            timesheet.UpdatedAt = DateTimeOffset.UtcNow;
            _db.TimesheetApprovalHistory.Add(History(timesheet.Id, TimesheetStatus.Approved, TimesheetStatus.Approved, userId, "Authorized adjustment applied"));
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("TimesheetAdjustmentReviewed", userId, true, adjustment.Id.ToString(), ipAddress, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult<AdjustmentDto>.Ok(MapAdjustment(adjustment));
    }

    public async Task<OperationResult<OvertimePolicyDto>> SavePolicyAsync(OvertimePolicyRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        if (!await StaffingAccess.IsAdministratorAsync(_users, userId))
        {
            return OperationResult<OvertimePolicyDto>.Fail(403, "Forbidden", "Only an administrator can configure overtime policy.");
        }

        var code = request.JurisdictionCode.Trim().ToUpperInvariant();
        var policy = await _db.OvertimePolicies.FirstOrDefaultAsync(item => item.JurisdictionCode == code, cancellationToken);
        if (policy is null)
        {
            policy = new OvertimePolicy { Id = Guid.NewGuid(), JurisdictionCode = code, CreatedAt = DateTimeOffset.UtcNow };
            _db.OvertimePolicies.Add(policy);
        }

        policy.RegularHoursPerDay = request.RegularHoursPerDay;
        policy.RegularHoursPerPeriod = request.RegularHoursPerPeriod;
        policy.IsActive = true;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("OvertimePolicySaved", userId, true, policy.JurisdictionCode, ipAddress, cancellationToken);
        return OperationResult<OvertimePolicyDto>.Ok(new OvertimePolicyDto(policy.Id, policy.JurisdictionCode, policy.RegularHoursPerDay, policy.RegularHoursPerPeriod, policy.IsActive));
    }

    private async Task<OperationResult<TimesheetDto>> SaveEntryAsync(Guid timesheetId, Guid? entryId, TimesheetEntryRequest request, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var timesheet = await _db.Timesheets.FirstOrDefaultAsync(item => item.Id == timesheetId, cancellationToken);
        if (timesheet is null)
        {
            return OperationResult<TimesheetDto>.Fail(404, "Not Found", "Timesheet not found.");
        }

        if (timesheet.Status == TimesheetStatus.Approved)
        {
            return OperationResult<TimesheetDto>.Fail(409, "Conflict", "Approved timesheets can change only through an adjustment.");
        }

        if (timesheet.Status == TimesheetStatus.Submitted)
        {
            return OperationResult<TimesheetDto>.Fail(409, "Conflict", "Withdraw the submission by waiting for review before editing hours.");
        }

        var profile = await OwnProfileAsync(userId, cancellationToken);
        if (profile is null || profile.Id != timesheet.ClinicianProfileId)
        {
            return OperationResult<TimesheetDto>.Fail(403, "Forbidden", "Only the owning clinician can edit these hours.");
        }

        var attendance = await _db.AttendanceRecords.FirstOrDefaultAsync(item => item.ShiftAssignmentId == request.ShiftAssignmentId && item.ClinicianProfileId == profile.Id && item.AttendanceStatus == AttendanceStatus.Completed, cancellationToken);
        if (attendance?.ClockOutTime is null)
        {
            return OperationResult<TimesheetDto>.Fail(409, "Conflict", "Record a completed clock-in and clock-out before adding hours.");
        }

        var assignment = await _db.ShiftAssignments.FirstAsync(item => item.Id == request.ShiftAssignmentId, cancellationToken);
        var shift = await _db.Shifts.FirstAsync(item => item.Id == assignment.ShiftId, cancellationToken);
        if (shift.FacilityId != timesheet.FacilityId)
        {
            return OperationResult<TimesheetDto>.Fail(409, "Conflict", "That shift belongs to a different facility.");
        }

        if (request.StartTime < attendance.ClockInTime || request.EndTime > attendance.ClockOutTime)
        {
            return OperationResult<TimesheetDto>.Fail(400, "Validation", "Timesheet hours must fall inside the recorded attendance.");
        }

        var minutes = (decimal)(request.EndTime - request.StartTime).TotalMinutes;
        if (request.BreakMinutes >= minutes)
        {
            return OperationResult<TimesheetDto>.Fail(400, "Validation", "Break time must be shorter than the time worked.");
        }

        var duplicate = await _db.TimesheetEntries.AnyAsync(item => item.ShiftAssignmentId == request.ShiftAssignmentId && item.Id != entryId, cancellationToken);
        if (duplicate)
        {
            return OperationResult<TimesheetDto>.Fail(409, "Conflict", "That shift already has a timesheet entry.");
        }

        var facility = await _db.HealthcareFacilities.AsNoTracking().FirstAsync(item => item.Id == timesheet.FacilityId, cancellationToken);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(facility.TimeZoneId);
        TimesheetEntry entry;
        if (entryId is Guid existingId)
        {
            var found = await _db.TimesheetEntries.FirstOrDefaultAsync(item => item.Id == existingId && item.TimesheetId == timesheetId, cancellationToken);
            if (found is null)
            {
                return OperationResult<TimesheetDto>.Fail(404, "Not Found", "Timesheet entry not found.");
            }

            entry = found;
        }
        else
        {
            entry = new TimesheetEntry { Id = Guid.NewGuid(), TimesheetId = timesheetId };
            _db.TimesheetEntries.Add(entry);
        }

        entry.ShiftAssignmentId = request.ShiftAssignmentId;
        entry.StartTime = request.StartTime;
        entry.EndTime = request.EndTime;
        entry.BreakMinutes = request.BreakMinutes;
        entry.WorkDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(request.StartTime, zone).DateTime);
        entry.Notes = request.Notes?.Trim() ?? "";
        if (entry.WorkDate < timesheet.PeriodStart || entry.WorkDate > timesheet.PeriodEnd)
        {
            return OperationResult<TimesheetDto>.Fail(400, "Validation", "The work date is outside the timesheet period.");
        }

        var entries = await _db.TimesheetEntries.Where(item => item.TimesheetId == timesheetId).ToListAsync(cancellationToken);
        if (!entries.Contains(entry))
        {
            entries.Add(entry);
        }

        var calculated = await RecalculateAsync(timesheet, entries, cancellationToken);
        if (calculated is not null)
        {
            return calculated;
        }

        timesheet.UpdatedAt = DateTimeOffset.UtcNow;
        if (timesheet.Status == TimesheetStatus.Rejected)
        {
            timesheet.Status = TimesheetStatus.Draft;
            timesheet.RejectionReason = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("TimesheetEntrySaved", userId, true, entry.Id.ToString(), ipAddress, cancellationToken);
        return OperationResult<TimesheetDto>.Ok(await MapTimesheetAsync(timesheet, false, cancellationToken));
    }

    private async Task<OperationResult<TimesheetDto>> ReviewAsync(Guid id, TimesheetStatus next, string? reason, string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await LockAsync(id, cancellationToken);
        var timesheet = await _db.Timesheets.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (timesheet is null)
        {
            return OperationResult<TimesheetDto>.Fail(404, "Not Found", "Timesheet not found.");
        }

        if (!await CanReviewAsync(timesheet, userId, cancellationToken))
        {
            return OperationResult<TimesheetDto>.Fail(403, "Forbidden", "You cannot review this timesheet.");
        }

        if (timesheet.Status == next)
        {
            await transaction.CommitAsync(cancellationToken);
            return OperationResult<TimesheetDto>.Ok(await MapTimesheetAsync(timesheet, false, cancellationToken));
        }

        if (timesheet.Status != TimesheetStatus.Submitted)
        {
            return OperationResult<TimesheetDto>.Fail(409, "Conflict", "Submit the timesheet before it can be reviewed.");
        }

        var previous = timesheet.Status;
        timesheet.Status = next;
        timesheet.UpdatedAt = DateTimeOffset.UtcNow;
        if (next == TimesheetStatus.Approved)
        {
            timesheet.ApprovedBy = userId;
            timesheet.ApprovedAt = DateTimeOffset.UtcNow;
            timesheet.RejectionReason = null;
        }
        else
        {
            timesheet.RejectionReason = reason;
            timesheet.ApprovedBy = null;
            timesheet.ApprovedAt = null;
        }

        _db.TimesheetApprovalHistory.Add(History(id, previous, next, userId, reason ?? "Approved"));
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult<TimesheetDto>.Fail(409, "Conflict", "This timesheet was reviewed by someone else.");
        }

        await _audit.WriteAsync(next == TimesheetStatus.Approved ? "TimesheetApproved" : "TimesheetRejected", userId, true, id.ToString(), ipAddress, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult<TimesheetDto>.Ok(await MapTimesheetAsync(timesheet, false, cancellationToken));
    }

    private async Task<OperationResult<TimesheetDto>?> RecalculateAsync(Timesheet timesheet, List<TimesheetEntry> entries, CancellationToken cancellationToken)
    {
        var policy = await PolicyForAsync(timesheet.FacilityId, cancellationToken);
        if (policy is null)
        {
            return OperationResult<TimesheetDto>.Fail(409, "Conflict", "Configure an overtime policy for this facility's jurisdiction.");
        }

        foreach (var entry in entries)
        {
            var worked = Hours(entry.StartTime, entry.EndTime, entry.BreakMinutes);
            var split = Split(worked, policy);
            entry.RegularHours = split.Regular;
            entry.OvertimeHours = split.Overtime;
        }

        ApplyPeriodCap(entries.OrderBy(item => item.StartTime).ToList(), policy.RegularHoursPerPeriod);
        timesheet.TotalRegularHours = entries.Sum(item => item.RegularHours);
        timesheet.TotalOvertimeHours = entries.Sum(item => item.OvertimeHours);
        return null;
    }

    private async Task<OvertimePolicy?> PolicyForAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        var state = await _db.HealthcareFacilities.Where(item => item.Id == facilityId).Select(item => item.State).FirstAsync(cancellationToken);
        var policy = await _db.OvertimePolicies.FirstOrDefaultAsync(item => item.JurisdictionCode == state && item.IsActive, cancellationToken);
        return policy ?? await _db.OvertimePolicies.FirstOrDefaultAsync(item => item.JurisdictionCode == FederalCode && item.IsActive, cancellationToken);
    }

    private static decimal Hours(DateTimeOffset start, DateTimeOffset end, int breakMinutes)
    {
        var minutes = (decimal)(end - start).TotalMinutes - breakMinutes;
        return minutes <= 0 ? 0 : Math.Round(minutes / 60m, 2, MidpointRounding.AwayFromZero);
    }

    private static (decimal Regular, decimal Overtime) Split(decimal worked, OvertimePolicy policy)
    {
        var regular = worked <= policy.RegularHoursPerDay ? worked : policy.RegularHoursPerDay;
        return (regular, worked - regular);
    }

    private static void ApplyPeriodCap(List<TimesheetEntry> entries, decimal periodCap)
    {
        var excess = entries.Sum(item => item.RegularHours) - periodCap;
        for (var index = entries.Count - 1; index >= 0 && excess > 0; index--)
        {
            var move = Math.Min(entries[index].RegularHours, excess);
            entries[index].RegularHours -= move;
            entries[index].OvertimeHours += move;
            excess -= move;
        }
    }

    private async Task<bool> CanReviewAsync(Timesheet timesheet, string userId, CancellationToken cancellationToken)
    {
        var owner = await _db.ClinicianProfiles.AsNoTracking().Where(item => item.Id == timesheet.ClinicianProfileId).Select(item => item.UserId).FirstAsync(cancellationToken);
        if (owner == userId)
        {
            return false;
        }

        return await StaffingAccess.CanManageFacilityAsync(_users, _db, userId, timesheet.FacilityId, cancellationToken);
    }

    private async Task<bool> CanSeeTimesheetAsync(Timesheet timesheet, string userId, CancellationToken cancellationToken)
    {
        var owner = await _db.ClinicianProfiles.AsNoTracking().Where(item => item.Id == timesheet.ClinicianProfileId).Select(item => item.UserId).FirstAsync(cancellationToken);
        if (owner == userId)
        {
            return true;
        }

        if (await StaffingAccess.CanViewFacilityAsync(_users, _db, userId, timesheet.FacilityId, cancellationToken))
        {
            return true;
        }

        return await _db.JobRequisitions.AnyAsync(item => item.FacilityId == timesheet.FacilityId && item.AssignedRecruiterId == userId, cancellationToken);
    }

    private async Task<IQueryable<Timesheet>> ScopeTimesheetsAsync(IQueryable<Timesheet> timesheets, string userId, CancellationToken cancellationToken)
    {
        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        if (access.IsAdministrator)
        {
            return timesheets;
        }

        if (access.IsClinicianOnly)
        {
            return timesheets.Where(item => _db.ClinicianProfiles.Any(profile => profile.Id == item.ClinicianProfileId && profile.UserId == userId));
        }

        var facilities = access.ManagedFacilityIds.Concat(access.RecruiterFacilityIds).ToArray();
        return timesheets.Where(item =>
            facilities.Contains(item.FacilityId)
            || _db.JobRequisitions.Any(requisition => requisition.FacilityId == item.FacilityId && requisition.AssignedRecruiterId == userId));
    }

    private async Task<List<Guid>> VisibleAssignmentsAsync(string userId, CancellationToken cancellationToken)
    {
        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        var query = from assignment in _db.ShiftAssignments.AsNoTracking()
                    join shift in _db.Shifts.AsNoTracking() on assignment.ShiftId equals shift.Id
                    select new { assignment, shift };
        if (access.IsAdministrator)
        {
            return await query.Select(item => item.assignment.Id).ToListAsync(cancellationToken);
        }

        if (access.IsClinicianOnly)
        {
            return await query.Where(item => _db.ClinicianProfiles.Any(profile => profile.Id == item.assignment.ClinicianProfileId && profile.UserId == userId))
                .Select(item => item.assignment.Id)
                .ToListAsync(cancellationToken);
        }

        var facilities = access.ManagedFacilityIds.Concat(access.RecruiterFacilityIds).ToArray();
        return await query.Where(item =>
                facilities.Contains(item.shift.FacilityId)
                || _db.JobRequisitions.Any(requisition => requisition.Id == item.shift.JobRequisitionId && requisition.AssignedRecruiterId == userId))
            .Select(item => item.assignment.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<(Guid ProfileId, OperationResult<AttendanceDto>? Error)> LoadAssignmentAsync(Guid assignmentId, string userId, CancellationToken cancellationToken)
    {
        var assignment = await _db.ShiftAssignments.AsNoTracking().FirstOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
        if (assignment is null)
        {
            return (Guid.Empty, OperationResult<AttendanceDto>.Fail(404, "Not Found", "Assignment not found."));
        }

        var profile = await _db.ClinicianProfiles.AsNoTracking().FirstAsync(item => item.Id == assignment.ClinicianProfileId, cancellationToken);
        if (profile.UserId != userId)
        {
            return (profile.Id, OperationResult<AttendanceDto>.Fail(403, "Forbidden", "Only the assigned clinician can record this attendance."));
        }

        if (assignment.AssignmentStatus != AssignmentStatus.Confirmed)
        {
            return (profile.Id, OperationResult<AttendanceDto>.Fail(409, "Conflict", "Confirm the assignment before recording attendance."));
        }

        return (profile.Id, null);
    }

    private async Task<ClinicianProfile?> OwnProfileAsync(string userId, CancellationToken cancellationToken) =>
        await _db.ClinicianProfiles.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

    private async Task<bool> OverlapsAsync(Guid profileId, Guid exceptId, DateTimeOffset start, DateTimeOffset? end, CancellationToken cancellationToken)
    {
        var others = await _db.AttendanceRecords.AsNoTracking()
            .Where(item => item.ClinicianProfileId == profileId && item.Id != exceptId)
            .ToListAsync(cancellationToken);
        var close = end ?? DateTimeOffset.MaxValue;
        return others.Any(item => item.ClockInTime < close && (item.ClockOutTime ?? DateTimeOffset.MaxValue) > start);
    }

    private async Task LockAsync(Guid id, CancellationToken cancellationToken)
    {
        var key = BitConverter.ToInt64(id.ToByteArray(), 0);
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }

    private async Task<AttendanceDto> MapAttendanceAsync(AttendanceRecord record, bool created, CancellationToken cancellationToken)
    {
        var profile = await _db.ClinicianProfiles.AsNoTracking().FirstAsync(item => item.Id == record.ClinicianProfileId, cancellationToken);
        var facilityId = await (
            from assignment in _db.ShiftAssignments.AsNoTracking()
            join shift in _db.Shifts.AsNoTracking() on assignment.ShiftId equals shift.Id
            where assignment.Id == record.ShiftAssignmentId
            select shift.FacilityId).FirstAsync(cancellationToken);
        return new AttendanceDto(record.Id, record.ShiftAssignmentId, record.ClinicianProfileId, $"{profile.FirstName} {profile.LastName}", facilityId, record.ClockInTime, record.ClockOutTime, record.AttendanceStatus.ToString(), created);
    }

    private async Task<TimesheetDto> MapTimesheetAsync(Timesheet timesheet, bool created, CancellationToken cancellationToken)
    {
        var profile = await _db.ClinicianProfiles.AsNoTracking().FirstAsync(item => item.Id == timesheet.ClinicianProfileId, cancellationToken);
        var facility = await _db.HealthcareFacilities.AsNoTracking().FirstAsync(item => item.Id == timesheet.FacilityId, cancellationToken);
        var entries = await _db.TimesheetEntries.AsNoTracking().Where(item => item.TimesheetId == timesheet.Id).OrderBy(item => item.StartTime)
            .Select(item => new TimesheetEntryDto(item.Id, item.ShiftAssignmentId, item.WorkDate, item.StartTime, item.EndTime, item.BreakMinutes, item.RegularHours, item.OvertimeHours, item.Notes))
            .ToListAsync(cancellationToken);
        return new TimesheetDto(
            timesheet.Id,
            timesheet.ClinicianProfileId,
            $"{profile.FirstName} {profile.LastName}",
            timesheet.FacilityId,
            facility.Name,
            timesheet.PeriodStart,
            timesheet.PeriodEnd,
            timesheet.TotalRegularHours,
            timesheet.TotalOvertimeHours,
            timesheet.Status.ToString(),
            timesheet.SubmittedAt,
            timesheet.ApprovedBy,
            timesheet.ApprovedAt,
            timesheet.RejectionReason,
            created,
            entries);
    }

    private static TimesheetApprovalHistory History(Guid timesheetId, TimesheetStatus previous, TimesheetStatus next, string userId, string comments) => new()
    {
        Id = Guid.NewGuid(),
        TimesheetId = timesheetId,
        PreviousStatus = previous,
        NewStatus = next,
        ChangedBy = userId,
        ChangedAt = DateTimeOffset.UtcNow,
        Comments = comments
    };

    private static AdjustmentDto MapAdjustment(TimesheetAdjustment adjustment) => new(
        adjustment.Id,
        adjustment.TimesheetEntryId,
        adjustment.OriginalHours,
        adjustment.AdjustedHours,
        adjustment.Reason,
        adjustment.Status.ToString(),
        adjustment.ReviewedBy,
        adjustment.ReviewedAt);
}
