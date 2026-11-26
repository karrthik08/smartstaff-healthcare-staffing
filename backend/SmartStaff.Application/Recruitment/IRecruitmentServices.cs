using SmartStaff.Application.Common;
using SmartStaff.Application.Staffing;

namespace SmartStaff.Application.Recruitment;

public interface IClinicianService
{
    Task<OperationResult<ClinicianProfileDto>> GetMineAsync(string userId, CancellationToken cancellationToken);

    Task<OperationResult<ClinicianProfileDto>> SaveMineAsync(ClinicianProfileRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<PageDto<ClinicianProfileDto>>> ListAsync(ClinicianListQuery query, string userId, CancellationToken cancellationToken);

    Task<OperationResult<ClinicianProfileDto>> GetAsync(Guid id, string userId, CancellationToken cancellationToken);

    Task<OperationResult<CredentialDto>> AddCredentialAsync(CredentialRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<IReadOnlyList<CredentialDto>>> ListCredentialsAsync(string userId, CancellationToken cancellationToken);

    Task<OperationResult<CredentialDto>> UpdateCredentialAsync(Guid id, CredentialRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<bool>> DeleteCredentialAsync(Guid id, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<CredentialDto>> VerifyCredentialAsync(Guid profileId, Guid credentialId, CredentialVerificationRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);
}

public interface IApplicationService
{
    Task<OperationResult<ApplicationDto>> ApplyAsync(ApplicationRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<PageDto<ApplicationDto>>> ListMineAsync(ApplicationListQuery query, string userId, CancellationToken cancellationToken);

    Task<OperationResult<PageDto<ApplicationDto>>> ListAsync(ApplicationListQuery query, string userId, CancellationToken cancellationToken);

    Task<OperationResult<ApplicationDto>> GetAsync(Guid id, string userId, CancellationToken cancellationToken);

    Task<OperationResult<ApplicationDto>> ChangeStatusAsync(Guid id, ApplicationStatusRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<RecruitmentSummaryDto>> SummaryAsync(string userId, CancellationToken cancellationToken);
}

public interface IPlacementService
{
    Task<OperationResult<PlacementDto>> CreateAsync(PlacementRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<PageDto<PlacementDto>>> ListAsync(PlacementListQuery query, string userId, CancellationToken cancellationToken);

    Task<OperationResult<PlacementDto>> GetAsync(Guid id, string userId, CancellationToken cancellationToken);

    Task<OperationResult<PlacementDto>> ChangeStatusAsync(Guid id, PlacementStatusRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);
}
