using SmartStaff.Application.Common;

namespace SmartStaff.Application.Analytics;

public interface IAnalyticsService
{
    Task<OperationResult<AnalyticsOverviewDto>> OverviewAsync(string userId, AnalyticsQuery query, CancellationToken cancellationToken);

    Task<OperationResult<RecruitmentAnalyticsDto>> RecruitmentAsync(string userId, AnalyticsQuery query, CancellationToken cancellationToken);

    Task<OperationResult<SchedulingAnalyticsDto>> SchedulingAsync(string userId, AnalyticsQuery query, CancellationToken cancellationToken);

    Task<OperationResult<AttendanceAnalyticsDto>> AttendanceAsync(string userId, AnalyticsQuery query, CancellationToken cancellationToken);

    Task<OperationResult<TimesheetAnalyticsDto>> TimesheetsAsync(string userId, AnalyticsQuery query, CancellationToken cancellationToken);

    Task<OperationResult<CredentialAnalyticsDto>> CredentialsAsync(string userId, AnalyticsQuery query, CancellationToken cancellationToken);

    Task<OperationResult<AnalyticsExport>> ExportAsync(string userId, string report, AnalyticsQuery query, CancellationToken cancellationToken);
}

public sealed record AnalyticsQuery(DateOnly? From, DateOnly? To, Guid? FacilityId, string? RecruiterId, int Page, int PageSize);

public sealed record MetricPoint(string Label, decimal Value);

public sealed record ReportRow(string Label, string Status, string Value, string OccurredOn);

public sealed record ReportPage(IReadOnlyList<ReportRow> Items, int Page, int PageSize, int Total);

public sealed record AnalyticsOverviewDto(
    int OpenRequisitions,
    int PublishedPositions,
    int FilledPositions,
    int Placements,
    int ActiveApplications,
    int ScheduledShifts,
    decimal StaffingCoveragePercent,
    decimal ScheduledHours,
    decimal WorkedHours,
    int PendingApprovals,
    int ExpiringCredentials);

public sealed record RecruitmentAnalyticsDto(
    int OpenRequisitions,
    int PublishedPositions,
    int FilledPositions,
    int Placements,
    int ActiveApplications,
    IReadOnlyList<MetricPoint> ApplicationsByStatus,
    decimal AverageDaysToPlacement,
    decimal PlacementConversionPercent,
    IReadOnlyList<MetricPoint> Trend,
    ReportPage Details);

public sealed record SchedulingAnalyticsDto(
    int ScheduledShifts,
    int ConfirmedAssignments,
    int UnfilledShifts,
    decimal StaffingCoveragePercent,
    int Cancellations,
    int ClinicianAvailability,
    IReadOnlyList<MetricPoint> Trend,
    ReportPage Details);

public sealed record AttendanceAnalyticsDto(
    decimal ScheduledHours,
    decimal WorkedHours,
    int MissedShifts,
    int Exceptions,
    int CompletedShifts,
    IReadOnlyList<MetricPoint> Trend,
    ReportPage Details);

public sealed record TimesheetAnalyticsDto(
    int Draft,
    int Submitted,
    int Approved,
    int Rejected,
    int PendingApprovals,
    decimal AverageApprovalHours,
    IReadOnlyList<MetricPoint> Trend,
    ReportPage Details);

public sealed record CredentialAnalyticsDto(
    int Verified,
    int PendingVerification,
    int Expiring,
    int Expired,
    IReadOnlyList<MetricPoint> ByStatus,
    ReportPage Details);

public sealed record AnalyticsExport(string FileName, byte[] Content);
