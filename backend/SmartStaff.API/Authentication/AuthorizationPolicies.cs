using Microsoft.AspNetCore.Authorization;
using SmartStaff.Application.Authentication;

namespace SmartStaff.API.Authentication;

public static class AuthorizationPolicies
{
    public const string Administrator = "Administrator";
    public const string Recruiter = "Recruiter";
    public const string Clinician = "Clinician";
    public const string FacilityManager = "FacilityManager";

    public static void AddSmartStaffPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(Administrator, policy => policy.RequireRole(AppRoles.Administrator));
        options.AddPolicy(Recruiter, policy => policy.RequireRole(AppRoles.RecruiterAccess));
        options.AddPolicy(Clinician, policy => policy.RequireRole(AppRoles.ClinicianAccess));
        options.AddPolicy(FacilityManager, policy => policy.RequireRole(AppRoles.FacilityManagerAccess));
    }
}
