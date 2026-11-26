namespace SmartStaff.Application.Authentication;

public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string StaffingCoordinator = "StaffingCoordinator";
    public const string Clinician = "Clinician";
    public const string HospitalAdministrator = "HospitalAdministrator";

    public static readonly IReadOnlyList<string> All =
    [
        Administrator,
        StaffingCoordinator,
        Clinician,
        HospitalAdministrator
    ];

    public static bool IsKnown(string role) => All.Contains(role, StringComparer.Ordinal);
}
