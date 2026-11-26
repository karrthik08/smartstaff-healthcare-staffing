using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartStaff.Application.Analytics;
using SmartStaff.Application.Common;
using SmartStaff.Domain.Recruitment;
using SmartStaff.Domain.Scheduling;
using SmartStaff.Domain.Staffing;
using SmartStaff.Domain.Timekeeping;
using SmartStaff.Infrastructure.Identity;
using SmartStaff.Infrastructure.Persistence;
using SmartStaff.Infrastructure.Staffing;

namespace SmartStaff.Infrastructure.Analytics;

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public AnalyticsService(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<OperationResult<AnalyticsOverviewDto>> OverviewAsync(string userId, AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var scope = await ResolveAsync(userId, query, cancellationToken);
        if (!scope.Succeeded || scope.Value is null)
        {
            return OperationResult<AnalyticsOverviewDto>.Fail(scope.StatusCode, scope.Title, scope.Detail);
        }

        var recruitment = await RecruitmentCoreAsync(scope.Value, query, cancellationToken);
        var scheduling = await SchedulingCoreAsync(scope.Value, query, cancellationToken);
        var attendance = await AttendanceCoreAsync(scope.Value, query, cancellationToken);
        var timesheets = await TimesheetCoreAsync(scope.Value, query, cancellationToken);
        var credentials = await CredentialCoreAsync(scope.Value, query, cancellationToken);
        return OperationResult<AnalyticsOverviewDto>.Ok(new AnalyticsOverviewDto(
            recruitment.OpenRequisitions,
            recruitment.PublishedPositions,
            recruitment.FilledPositions,
            recruitment.Placements,
            recruitment.ActiveApplications,
            scheduling.ScheduledShifts,
            scheduling.StaffingCoveragePercent,
            attendance.ScheduledHours,
            attendance.WorkedHours,
            timesheets.PendingApprovals,
            credentials.Expiring));
    }

    public async Task<OperationResult<RecruitmentAnalyticsDto>> RecruitmentAsync(string userId, AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var scope = await ResolveAsync(userId, query, cancellationToken);
        if (!scope.Succeeded || scope.Value is null)
        {
            return OperationResult<RecruitmentAnalyticsDto>.Fail(scope.StatusCode, scope.Title, scope.Detail);
        }

        var core = await RecruitmentCoreAsync(scope.Value, query, cancellationToken);
        var applications = Applications(scope.Value, query);
        var byStatus = (await applications.GroupBy(item => item.Status)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken))
            .Select(item => new MetricPoint(item.Key.ToString(), item.Count))
            .ToList();
        var trendRows = await applications.Select(item => item.AppliedAt).ToListAsync(cancellationToken);
        var requisitionPage = Requisitions(scope.Value);
        requisitionPage = Apply(requisitionPage, query, item => item.CreatedAt);
        var details = await PageRowsAsync(requisitionPage.OrderByDescending(item => item.CreatedAt), query, item => new ReportRow(item.JobTitle, item.Status.ToString(), item.OpenPositions.ToString(), item.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd")), cancellationToken);
        return OperationResult<RecruitmentAnalyticsDto>.Ok(core with
        {
            ApplicationsByStatus = byStatus,
            Trend = Trend(trendRows),
            Details = details
        });
    }

    public async Task<OperationResult<SchedulingAnalyticsDto>> SchedulingAsync(string userId, AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var scope = await ResolveAsync(userId, query, cancellationToken);
        if (!scope.Succeeded || scope.Value is null)
        {
            return OperationResult<SchedulingAnalyticsDto>.Fail(scope.StatusCode, scope.Title, scope.Detail);
        }

        var core = await SchedulingCoreAsync(scope.Value, query, cancellationToken);
        var starts = await Shifts(scope.Value, query).Select(item => item.StartDateTime).ToListAsync(cancellationToken);
        var details = await PageRowsAsync(Shifts(scope.Value, query).OrderByDescending(item => item.StartDateTime), query, item => new ReportRow(item.Specialty, item.Status.ToString(), item.RequiredClinicians.ToString(), item.StartDateTime.UtcDateTime.ToString("yyyy-MM-dd")), cancellationToken);
        return OperationResult<SchedulingAnalyticsDto>.Ok(core with { Trend = Trend(starts), Details = details });
    }

    public async Task<OperationResult<AttendanceAnalyticsDto>> AttendanceAsync(string userId, AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var scope = await ResolveAsync(userId, query, cancellationToken);
        if (!scope.Succeeded || scope.Value is null)
        {
            return OperationResult<AttendanceAnalyticsDto>.Fail(scope.StatusCode, scope.Title, scope.Detail);
        }

        var core = await AttendanceCoreAsync(scope.Value, query, cancellationToken);
        var worked = await CompletedAttendance(scope.Value, query).Select(item => item.ClockInTime).ToListAsync(cancellationToken);
        var details = await PageRowsAsync(CompletedAttendance(scope.Value, query).OrderByDescending(item => item.ClockInTime), query, item => new ReportRow("Completed attendance", item.AttendanceStatus.ToString(), "", item.ClockInTime.UtcDateTime.ToString("yyyy-MM-dd")), cancellationToken);
        return OperationResult<AttendanceAnalyticsDto>.Ok(core with { Trend = Trend(worked), Details = details });
    }

    public async Task<OperationResult<TimesheetAnalyticsDto>> TimesheetsAsync(string userId, AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var scope = await ResolveAsync(userId, query, cancellationToken);
        if (!scope.Succeeded || scope.Value is null)
        {
            return OperationResult<TimesheetAnalyticsDto>.Fail(scope.StatusCode, scope.Title, scope.Detail);
        }

        var core = await TimesheetCoreAsync(scope.Value, query, cancellationToken);
        var submitted = await Sheets(scope.Value, query).Where(item => item.SubmittedAt != null).Select(item => item.SubmittedAt!.Value).ToListAsync(cancellationToken);
        var details = await PageRowsAsync(Sheets(scope.Value, query).OrderByDescending(item => item.PeriodStart), query, item => new ReportRow($"{item.PeriodStart:yyyy-MM-dd} to {item.PeriodEnd:yyyy-MM-dd}", item.Status.ToString(), $"{item.TotalRegularHours} regular", item.PeriodStart.ToString("yyyy-MM-dd")), cancellationToken);
        return OperationResult<TimesheetAnalyticsDto>.Ok(core with { Trend = Trend(submitted), Details = details });
    }

    public async Task<OperationResult<CredentialAnalyticsDto>> CredentialsAsync(string userId, AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var scope = await ResolveAsync(userId, query, cancellationToken);
        if (!scope.Succeeded || scope.Value is null)
        {
            return OperationResult<CredentialAnalyticsDto>.Fail(scope.StatusCode, scope.Title, scope.Detail);
        }

        var core = await CredentialCoreAsync(scope.Value, query, cancellationToken);
        var byStatus = (await Credentials(scope.Value, query).GroupBy(item => item.VerificationStatus)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken))
            .Select(item => new MetricPoint(item.Key.ToString(), item.Count))
            .ToList();
        var details = await PageRowsAsync(Credentials(scope.Value, query).OrderBy(item => item.ExpirationDate), query, item => new ReportRow(item.CredentialType.ToString(), item.VerificationStatus.ToString(), item.ExpirationDate?.ToString("yyyy-MM-dd") ?? "", item.IssueDate.ToString("yyyy-MM-dd")), cancellationToken);
        return OperationResult<CredentialAnalyticsDto>.Ok(core with { ByStatus = byStatus, Details = details });
    }

    public async Task<OperationResult<AnalyticsExport>> ExportAsync(string userId, string report, AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var rows = new List<string[]> { new[] { "section", "label", "status", "value", "occurredOn" } };
        switch (report)
        {
            case "recruitment":
                var recruitment = await RecruitmentAsync(userId, query, cancellationToken);
                if (!recruitment.Succeeded || recruitment.Value is null) return OperationResult<AnalyticsExport>.Fail(recruitment.StatusCode, recruitment.Title, recruitment.Detail);
                rows.Add(Metric("summary", "openRequisitions", recruitment.Value.OpenRequisitions));
                rows.Add(Metric("summary", "publishedPositions", recruitment.Value.PublishedPositions));
                rows.Add(Metric("summary", "filledPositions", recruitment.Value.FilledPositions));
                rows.Add(Metric("summary", "placements", recruitment.Value.Placements));
                rows.Add(Metric("summary", "activeApplications", recruitment.Value.ActiveApplications));
                rows.Add(Metric("summary", "averageDaysToPlacement", recruitment.Value.AverageDaysToPlacement));
                rows.Add(Metric("summary", "placementConversionPercent", recruitment.Value.PlacementConversionPercent));
                rows.AddRange(recruitment.Value.Details.Items.Select(item => Detail("requisition", item)));
                break;
            case "scheduling":
                var scheduling = await SchedulingAsync(userId, query, cancellationToken);
                if (!scheduling.Succeeded || scheduling.Value is null) return OperationResult<AnalyticsExport>.Fail(scheduling.StatusCode, scheduling.Title, scheduling.Detail);
                rows.Add(Metric("summary", "scheduledShifts", scheduling.Value.ScheduledShifts));
                rows.Add(Metric("summary", "confirmedAssignments", scheduling.Value.ConfirmedAssignments));
                rows.Add(Metric("summary", "unfilledShifts", scheduling.Value.UnfilledShifts));
                rows.Add(Metric("summary", "staffingCoveragePercent", scheduling.Value.StaffingCoveragePercent));
                rows.AddRange(scheduling.Value.Details.Items.Select(item => Detail("shift", item)));
                break;
            case "attendance":
                var attendance = await AttendanceAsync(userId, query, cancellationToken);
                if (!attendance.Succeeded || attendance.Value is null) return OperationResult<AnalyticsExport>.Fail(attendance.StatusCode, attendance.Title, attendance.Detail);
                rows.Add(Metric("summary", "scheduledHours", attendance.Value.ScheduledHours));
                rows.Add(Metric("summary", "workedHours", attendance.Value.WorkedHours));
                rows.Add(Metric("summary", "missedShifts", attendance.Value.MissedShifts));
                rows.AddRange(attendance.Value.Details.Items.Select(item => Detail("attendance", item)));
                break;
            case "timesheets":
                var timesheets = await TimesheetsAsync(userId, query, cancellationToken);
                if (!timesheets.Succeeded || timesheets.Value is null) return OperationResult<AnalyticsExport>.Fail(timesheets.StatusCode, timesheets.Title, timesheets.Detail);
                rows.Add(Metric("summary", "approved", timesheets.Value.Approved));
                rows.Add(Metric("summary", "pendingApprovals", timesheets.Value.PendingApprovals));
                rows.AddRange(timesheets.Value.Details.Items.Select(item => Detail("timesheet", item)));
                break;
            case "credentials":
                var credentials = await CredentialsAsync(userId, query, cancellationToken);
                if (!credentials.Succeeded || credentials.Value is null) return OperationResult<AnalyticsExport>.Fail(credentials.StatusCode, credentials.Title, credentials.Detail);
                rows.Add(Metric("summary", "verified", credentials.Value.Verified));
                rows.Add(Metric("summary", "pendingVerification", credentials.Value.PendingVerification));
                rows.Add(Metric("summary", "expiring", credentials.Value.Expiring));
                rows.Add(Metric("summary", "expired", credentials.Value.Expired));
                rows.AddRange(credentials.Value.Details.Items.Select(item => Detail("credential", item)));
                break;
            default:
                return OperationResult<AnalyticsExport>.Fail(400, "Unknown report", "Choose recruitment, scheduling, attendance, timesheets, or credentials.");
        }

        return OperationResult<AnalyticsExport>.Ok(new AnalyticsExport($"smartstaff-{report}.csv", CsvSafety.Build(rows)));
    }

    private async Task<RecruitmentAnalyticsDto> RecruitmentCoreAsync(Scope scope, AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var requisitions = Apply(Requisitions(scope), query, item => item.CreatedAt);
        var applications = Applications(scope, query);
        var placements = Placements(scope, query);
        var placed = await applications.CountAsync(item => item.Status == ApplicationStatus.Placed, cancellationToken);
        var considered = await applications.CountAsync(item => item.Status != ApplicationStatus.Withdrawn, cancellationToken);
        var spans = await placements.Select(item => new { item.CreatedAt, item.Application.AppliedAt }).ToListAsync(cancellationToken);
        var averageDays = spans.Count == 0 ? 0 : Math.Round((decimal)spans.Average(item => (item.CreatedAt - item.AppliedAt).TotalDays), 2);
        return new RecruitmentAnalyticsDto(
            await requisitions.CountAsync(item => item.Status == RequisitionStatus.Open || item.Status == RequisitionStatus.Published, cancellationToken),
            await requisitions.CountAsync(item => item.Status == RequisitionStatus.Published, cancellationToken),
            await requisitions.CountAsync(item => item.Status == RequisitionStatus.Filled, cancellationToken),
            await placements.CountAsync(cancellationToken),
            await applications.CountAsync(item => item.Status != ApplicationStatus.Rejected && item.Status != ApplicationStatus.Withdrawn && item.Status != ApplicationStatus.Placed, cancellationToken),
            [],
            averageDays,
            considered == 0 ? 0 : Math.Round(placed * 100m / considered, 2),
            [],
            new ReportPage([], query.Page, query.PageSize, 0));
    }

    private async Task<SchedulingAnalyticsDto> SchedulingCoreAsync(Scope scope, AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var shifts = await Shifts(scope, query).Select(item => new { item.Id, item.Status, item.RequiredClinicians, item.StartDateTime, item.EndDateTime }).ToListAsync(cancellationToken);
        var ids = shifts.Select(item => item.Id).ToArray();
        var assignments = await _db.ShiftAssignments.AsNoTracking().Where(item => ids.Contains(item.ShiftId))
            .Select(item => new { item.ShiftId, item.AssignmentStatus }).ToListAsync(cancellationToken);
        var confirmed = assignments.Count(item => item.AssignmentStatus == AssignmentStatus.Confirmed);
        var coverageRows = shifts.Where(item => item.Status != ShiftScheduleStatus.Cancelled).Select(item => (
            item.RequiredClinicians,
            assignments.Count(assignment => assignment.ShiftId == item.Id && assignment.AssignmentStatus == AssignmentStatus.Confirmed)));
        var unfilled = shifts.Count(item => item.Status != ShiftScheduleStatus.Cancelled && assignments.Count(assignment => assignment.ShiftId == item.Id && assignment.AssignmentStatus != AssignmentStatus.Cancelled) < item.RequiredClinicians);
        var availability = Availability(scope, query);
        return new SchedulingAnalyticsDto(
            shifts.Count(item => item.Status != ShiftScheduleStatus.Cancelled),
            confirmed,
            unfilled,
            Coverage(coverageRows),
            shifts.Count(item => item.Status == ShiftScheduleStatus.Cancelled),
            await availability.CountAsync(cancellationToken),
            [],
            new ReportPage([], query.Page, query.PageSize, 0));
    }

    private async Task<AttendanceAnalyticsDto> AttendanceCoreAsync(Scope scope, AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var shifts = await Shifts(scope, query).Where(item => item.Status != ShiftScheduleStatus.Cancelled)
            .Select(item => new { item.Id, item.StartDateTime, item.EndDateTime }).ToListAsync(cancellationToken);
        var ids = shifts.Select(item => item.Id).ToArray();
        var assignments = await _db.ShiftAssignments.AsNoTracking().Where(item => ids.Contains(item.ShiftId) && item.AssignmentStatus == AssignmentStatus.Confirmed)
            .Select(item => new { item.Id, item.ShiftId }).ToListAsync(cancellationToken);
        var assignmentIds = assignments.Select(item => item.Id).ToArray();
        var attendance = await _db.AttendanceRecords.AsNoTracking().Where(item => assignmentIds.Contains(item.ShiftAssignmentId))
            .Select(item => new { item.ShiftAssignmentId, item.AttendanceStatus, item.ClockInTime, item.ClockOutTime }).ToListAsync(cancellationToken);
        var scheduled = assignments.Sum(item =>
        {
            var shift = shifts.First(row => row.Id == item.ShiftId);
            return (decimal)(shift.EndDateTime - shift.StartDateTime).TotalHours;
        });
        var worked = attendance.Where(item => item.AttendanceStatus == AttendanceStatus.Completed && item.ClockOutTime != null)
            .Sum(item => (decimal)(item.ClockOutTime!.Value - item.ClockInTime).TotalHours);
        var now = DateTimeOffset.UtcNow;
        var missed = assignments.Count(item =>
        {
            var shift = shifts.First(row => row.Id == item.ShiftId);
            return shift.EndDateTime < now && !attendance.Any(record => record.ShiftAssignmentId == item.Id && record.AttendanceStatus == AttendanceStatus.Completed);
        });
        var openLate = attendance.Count(item => item.AttendanceStatus == AttendanceStatus.Open && shifts.Any(shift => assignments.Any(assignment => assignment.Id == item.ShiftAssignmentId && assignment.ShiftId == shift.Id && shift.EndDateTime < now)));
        var exceptions = openLate + await (
            from change in _db.ShiftChangeRequests.AsNoTracking()
            join assignment in _db.ShiftAssignments.AsNoTracking() on change.ShiftAssignmentId equals assignment.Id
            where assignmentIds.Contains(assignment.Id) && change.Status == ShiftChangeStatus.Pending
            select change.Id).CountAsync(cancellationToken);
        return new AttendanceAnalyticsDto(Math.Round(scheduled, 2), Math.Round(worked, 2), missed, exceptions, attendance.Count(item => item.AttendanceStatus == AttendanceStatus.Completed), [], new ReportPage([], query.Page, query.PageSize, 0));
    }

    private async Task<TimesheetAnalyticsDto> TimesheetCoreAsync(Scope scope, AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var sheets = Sheets(scope, query);
        var approved = await sheets.Where(item => item.Status == TimesheetStatus.Approved && item.SubmittedAt != null && item.ApprovedAt != null)
            .Select(item => new { item.SubmittedAt, item.ApprovedAt }).ToListAsync(cancellationToken);
        var average = approved.Count == 0 ? 0 : Math.Round((decimal)approved.Average(item => (item.ApprovedAt!.Value - item.SubmittedAt!.Value).TotalHours), 2);
        return new TimesheetAnalyticsDto(
            await sheets.CountAsync(item => item.Status == TimesheetStatus.Draft, cancellationToken),
            await sheets.CountAsync(item => item.Status == TimesheetStatus.Submitted, cancellationToken),
            await sheets.CountAsync(item => item.Status == TimesheetStatus.Approved, cancellationToken),
            await sheets.CountAsync(item => item.Status == TimesheetStatus.Rejected, cancellationToken),
            await sheets.CountAsync(item => item.Status == TimesheetStatus.Submitted, cancellationToken),
            average,
            [],
            new ReportPage([], query.Page, query.PageSize, 0));
    }

    private async Task<CredentialAnalyticsDto> CredentialCoreAsync(Scope scope, AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var soon = today.AddDays(30);
        var credentials = Credentials(scope, query);
        return new CredentialAnalyticsDto(
            await credentials.CountAsync(item => item.VerificationStatus == VerificationStatus.Verified, cancellationToken),
            await credentials.CountAsync(item => item.VerificationStatus == VerificationStatus.Pending, cancellationToken),
            await credentials.CountAsync(item => item.ExpirationDate != null && item.ExpirationDate >= today && item.ExpirationDate <= soon, cancellationToken),
            await credentials.CountAsync(item => item.ExpirationDate != null && item.ExpirationDate < today, cancellationToken),
            [],
            new ReportPage([], query.Page, query.PageSize, 0));
    }

    private IQueryable<JobRequisition> Requisitions(Scope scope)
    {
        var query = _db.JobRequisitions.AsNoTracking().Where(item => scope.FacilityIds == null || scope.FacilityIds.Contains(item.FacilityId));
        if (scope.Personal)
        {
            query = query.Where(item => _db.JobApplications.Any(application => application.JobRequisitionId == item.Id && application.ClinicianProfileId == scope.ProfileId));
        }
        else if (scope.RecruiterUserId is not null)
        {
            query = query.Where(item => item.AssignedRecruiterId == scope.RecruiterUserId);
        }

        return query;
    }

    private IQueryable<JobApplication> Applications(Scope scope, AnalyticsQuery query)
    {
        var requisitionIds = Requisitions(scope).Select(item => item.Id);
        return Apply(_db.JobApplications.AsNoTracking().Where(item => requisitionIds.Contains(item.JobRequisitionId) && (!scope.Personal || item.ClinicianProfileId == scope.ProfileId)), query, item => item.AppliedAt);
    }

    private IQueryable<Placement> Placements(Scope scope, AnalyticsQuery query)
    {
        var requisitionIds = Requisitions(scope).Select(item => item.Id);
        return Apply(_db.Placements.AsNoTracking().Where(item => requisitionIds.Contains(item.Application.JobRequisitionId) && (!scope.Personal || item.ClinicianProfileId == scope.ProfileId)), query, item => item.CreatedAt);
    }

    private IQueryable<Shift> Shifts(Scope scope, AnalyticsQuery query)
    {
        var requisitionIds = Requisitions(scope).Select(item => item.Id);
        var shifts = Apply(_db.Shifts.AsNoTracking().Where(item => requisitionIds.Contains(item.JobRequisitionId)), query, item => item.StartDateTime);
        if (scope.Personal)
        {
            shifts = shifts.Where(item => _db.ShiftAssignments.Any(assignment => assignment.ShiftId == item.Id && assignment.ClinicianProfileId == scope.ProfileId && assignment.AssignmentStatus != AssignmentStatus.Cancelled));
        }

        return shifts;
    }

    private IQueryable<ClinicianAvailability> Availability(Scope scope, AnalyticsQuery query)
    {
        var from = Start(query);
        var to = End(query);
        var rows = _db.ClinicianAvailability.AsNoTracking().Where(item => item.AvailabilityType == AvailabilityType.Available);
        if (scope.Personal)
        {
            rows = rows.Where(item => item.ClinicianProfileId == scope.ProfileId);
        }
        else
        {
            var profiles = Placements(scope, new AnalyticsQuery(null, null, query.FacilityId, query.RecruiterId, 1, 10)).Select(item => item.ClinicianProfileId);
            rows = rows.Where(item => profiles.Contains(item.ClinicianProfileId));
        }

        if (from is not null)
        {
            rows = rows.Where(item => item.AvailableUntil >= from);
        }

        if (to is not null)
        {
            rows = rows.Where(item => item.AvailableFrom < to);
        }

        return rows;
    }

    private IQueryable<AttendanceRecord> CompletedAttendance(Scope scope, AnalyticsQuery query)
    {
        var shifts = Shifts(scope, query).Select(item => item.Id);
        return from record in _db.AttendanceRecords.AsNoTracking()
               join assignment in _db.ShiftAssignments.AsNoTracking() on record.ShiftAssignmentId equals assignment.Id
               where shifts.Contains(assignment.ShiftId) && record.AttendanceStatus == AttendanceStatus.Completed && (!scope.Personal || record.ClinicianProfileId == scope.ProfileId)
               select record;
    }

    private IQueryable<Timesheet> Sheets(Scope scope, AnalyticsQuery query)
    {
        var rows = _db.Timesheets.AsNoTracking().Where(item => scope.FacilityIds == null || scope.FacilityIds.Contains(item.FacilityId));
        if (scope.Personal)
        {
            rows = rows.Where(item => item.ClinicianProfileId == scope.ProfileId);
        }
        else if (scope.RecruiterUserId is not null)
        {
            var facilities = Requisitions(scope).Select(item => item.FacilityId);
            rows = rows.Where(item => facilities.Contains(item.FacilityId));
        }

        if (query.From is DateOnly from)
        {
            rows = rows.Where(item => item.PeriodEnd >= from);
        }

        if (query.To is DateOnly to)
        {
            rows = rows.Where(item => item.PeriodStart <= to);
        }

        return rows;
    }

    private IQueryable<ClinicianCredential> Credentials(Scope scope, AnalyticsQuery query)
    {
        var rows = _db.ClinicianCredentials.AsNoTracking();
        if (scope.Personal)
        {
            rows = rows.Where(item => item.ClinicianProfileId == scope.ProfileId);
        }
        else if (scope.RecruiterUserId is not null || scope.FacilityIds is not null)
        {
            var profiles = Placements(scope, new AnalyticsQuery(null, null, query.FacilityId, query.RecruiterId, 1, 10)).Select(item => item.ClinicianProfileId);
            rows = rows.Where(item => profiles.Contains(item.ClinicianProfileId));
        }

        if (query.From is DateOnly from)
        {
            rows = rows.Where(item => item.IssueDate >= from);
        }

        if (query.To is DateOnly to)
        {
            rows = rows.Where(item => item.IssueDate <= to);
        }

        return rows;
    }

    private async Task<OperationResult<Scope>> ResolveAsync(string userId, AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var access = await StaffingAccess.DescribeAsync(_users, _db, userId, cancellationToken);
        if (access.IsClinicianOnly)
        {
            var profileId = await _db.ClinicianProfiles.AsNoTracking().Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).FirstOrDefaultAsync(cancellationToken);
            if (query.FacilityId is Guid requested)
            {
                var allowed = profileId is not null && await _db.Placements.AnyAsync(item => item.ClinicianProfileId == profileId && item.FacilityId == requested, cancellationToken);
                if (!allowed)
                {
                    return OperationResult<Scope>.Fail(403, "Forbidden", "You can only view your own staffing metrics.");
                }
            }

            if (!string.IsNullOrWhiteSpace(query.RecruiterId))
            {
                return OperationResult<Scope>.Fail(403, "Forbidden", "You can only view your own staffing metrics.");
            }

            Guid[]? facilities = query.FacilityId is Guid facility ? [facility] : null;
            return OperationResult<Scope>.Ok(new Scope(true, profileId, facilities, null));
        }

        if (!string.IsNullOrWhiteSpace(query.RecruiterId) && !access.IsAdministrator && query.RecruiterId != userId)
        {
            return OperationResult<Scope>.Fail(403, "Forbidden", "You can only filter recruitment assigned to you.");
        }

        if (access.IsAdministrator)
        {
            Guid[]? facilities = query.FacilityId is Guid facility ? [facility] : null;
            var recruiter = string.IsNullOrWhiteSpace(query.RecruiterId) ? null : query.RecruiterId;
            return OperationResult<Scope>.Ok(new Scope(false, null, facilities, recruiter));
        }

        var managed = access.ManagedFacilityIds;
        var recruited = await _db.JobRequisitions.AsNoTracking().Where(item => item.AssignedRecruiterId == userId).Select(item => item.FacilityId).Distinct().ToListAsync(cancellationToken);
        var allowedFacilities = access.IsFacilityManager ? managed : recruited.ToArray();
        if (query.FacilityId is Guid requestedFacility && !allowedFacilities.Contains(requestedFacility))
        {
            return OperationResult<Scope>.Fail(403, "Forbidden", "That facility is outside your staffing access.");
        }

        var facilityIds = query.FacilityId is Guid selected ? new[] { selected } : allowedFacilities;
        var recruiterId = access.IsFacilityManager ? null : userId;
        return OperationResult<Scope>.Ok(new Scope(false, null, facilityIds, recruiterId));
    }

    private static async Task<ReportPage> PageRowsAsync<T>(IQueryable<T> query, AnalyticsQuery request, Func<T, ReportRow> map, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var size = request.PageSize is < 1 or > 50 ? 10 : request.PageSize;
        var total = await query.CountAsync(cancellationToken);
        var items = (await query.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken)).Select(map).ToList();
        return new ReportPage(items, page, size, total);
    }

    private static IQueryable<T> Apply<T>(IQueryable<T> query, AnalyticsQuery range, System.Linq.Expressions.Expression<Func<T, DateTimeOffset>> timestamp)
    {
        if (range.From is DateOnly from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var parameter = timestamp.Parameters[0];
            var body = System.Linq.Expressions.Expression.GreaterThanOrEqual(timestamp.Body, System.Linq.Expressions.Expression.Constant(start));
            query = query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parameter));
        }

        if (range.To is DateOnly to)
        {
            var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var parameter = timestamp.Parameters[0];
            var body = System.Linq.Expressions.Expression.LessThan(timestamp.Body, System.Linq.Expressions.Expression.Constant(end));
            query = query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parameter));
        }

        return query;
    }

    private static DateTimeOffset? Start(AnalyticsQuery query) =>
        query.From is DateOnly from ? new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;

    private static DateTimeOffset? End(AnalyticsQuery query) =>
        query.To is DateOnly to ? new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;

    private static decimal Coverage(IEnumerable<(int Required, int Confirmed)> rows)
    {
        var list = rows.ToList();
        var required = list.Sum(item => item.Required);
        if (required == 0)
        {
            return 0;
        }

        return Math.Round(list.Sum(item => Math.Min(item.Confirmed, item.Required)) * 100m / required, 2);
    }

    private static IReadOnlyList<MetricPoint> Trend(IEnumerable<DateTimeOffset> values) =>
        values.GroupBy(item => item.UtcDateTime.Date).OrderBy(group => group.Key).Take(60)
            .Select(group => new MetricPoint(group.Key.ToString("yyyy-MM-dd"), group.Count())).ToList();

    private static string[] Metric(string section, string label, decimal value) => [section, label, "", value.ToString(), ""];

    private static string[] Detail(string section, ReportRow row) => [section, row.Label, row.Status, row.Value, row.OccurredOn];

    private sealed record Scope(bool Personal, Guid? ProfileId, Guid[]? FacilityIds, string? RecruiterUserId);
}
