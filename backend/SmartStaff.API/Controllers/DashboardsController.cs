using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Authentication;

namespace SmartStaff.API.Controllers;

[ApiController]
[Route("api/dashboards")]
[Authorize]
public sealed class DashboardsController : ControllerBase
{
    [HttpGet("administrator")]
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    public IActionResult Administrator() => Workspace("administrator", "Administrator workspace", "You can manage users, assign roles, and review audit records.");

    [HttpGet("recruiter")]
    [Authorize(Policy = AuthorizationPolicies.Recruiter)]
    public IActionResult Recruiter() => Workspace("recruiter", "Recruiter workspace", "You can manage staffing operations for this account.");

    [HttpGet("coordinator")]
    [Authorize(Policy = AuthorizationPolicies.Recruiter)]
    public IActionResult Coordinator() => Recruiter();

    [HttpGet("clinician")]
    [Authorize(Policy = AuthorizationPolicies.Clinician)]
    public IActionResult Clinician() => Workspace("clinician", "Clinician workspace", "You can manage your personal profile and view your clinician workspace.");

    [HttpGet("facility")]
    [Authorize(Policy = AuthorizationPolicies.FacilityManager)]
    public IActionResult Facility() => Workspace("facility", "Facility manager workspace", "You can manage the facility operations authorized for this account.");

    [HttpGet("hospital")]
    [Authorize(Policy = AuthorizationPolicies.FacilityManager)]
    public IActionResult Hospital() => Facility();

    private IActionResult Workspace(string workspace, string title, string message) =>
        Ok(new DashboardDto(workspace, title, message, DateTimeOffset.UtcNow));
}
