using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.Application.Authentication;

namespace SmartStaff.API.Controllers;

[ApiController]
[Route("api/dashboards")]
[Authorize]
public sealed class DashboardsController : ControllerBase
{
    [HttpGet("administrator")]
    [Authorize(Roles = AppRoles.Administrator)]
    public IActionResult Administrator() => Workspace("administrator", "Administrator workspace", "You can manage users, assign roles, and review audit records.");

    [HttpGet("coordinator")]
    [Authorize(Roles = AppRoles.Administrator + "," + AppRoles.StaffingCoordinator)]
    public IActionResult Coordinator() => Workspace("coordinator", "Staffing coordinator workspace", "You can manage staffing operations for this account.");

    [HttpGet("clinician")]
    [Authorize(Roles = AppRoles.Administrator + "," + AppRoles.Clinician)]
    public IActionResult Clinician() => Workspace("clinician", "Clinician workspace", "You can manage your personal profile and view your clinician workspace.");

    [HttpGet("hospital")]
    [Authorize(Roles = AppRoles.Administrator + "," + AppRoles.HospitalAdministrator)]
    public IActionResult Hospital() => Workspace("hospital", "Hospital administrator workspace", "You can manage the hospital operations authorized for this account.");

    private IActionResult Workspace(string workspace, string title, string message) =>
        Ok(new DashboardDto(workspace, title, message, DateTimeOffset.UtcNow));
}
