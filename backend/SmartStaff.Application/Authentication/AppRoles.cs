namespace SmartStaff.Application.Authentication;

public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string Recruiter = "Recruiter";
    public const string Clinician = "Clinician";
    public const string FacilityManager = "FacilityManager";

    public const string LegacyStaffingCoordinator = "StaffingCoordinator";
    public const string LegacyStaffingCoordinatorLabel = "Staffing Coordinator";
    public const string LegacyHospitalAdministrator = "HospitalAdministrator";
    public const string LegacyHospitalAdministratorLabel = "Hospital Administrator";

    public static readonly string[] Assignable =
    [
        Administrator,
        Recruiter,
        Clinician,
        FacilityManager
    ];

    public static readonly string[] RecruiterAccess =
    [
        Administrator,
        Recruiter,
        LegacyStaffingCoordinator,
        LegacyStaffingCoordinatorLabel
    ];

    public static readonly string[] FacilityManagerAccess =
    [
        Administrator,
        FacilityManager,
        LegacyHospitalAdministrator,
        LegacyHospitalAdministratorLabel
    ];

    public static readonly string[] ClinicianAccess = [Administrator, Clinician];

    public static bool IsAssignable(string role) =>
        Assignable.Contains(role, StringComparer.Ordinal);

    public static IReadOnlyList<string> StoredNamesForFilter(string role)
    {
        if (role is Recruiter or LegacyStaffingCoordinator or LegacyStaffingCoordinatorLabel)
        {
            return [Recruiter, LegacyStaffingCoordinator, LegacyStaffingCoordinatorLabel];
        }

        if (role is FacilityManager or LegacyHospitalAdministrator or LegacyHospitalAdministratorLabel)
        {
            return [FacilityManager, LegacyHospitalAdministrator, LegacyHospitalAdministratorLabel];
        }

        return [role];
    }
}
