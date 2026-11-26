using SmartStaff.Application.Common;

namespace SmartStaff.Application.Staffing;

public interface IFacilityService
{
    Task<OperationResult<FacilityDto>> CreateAsync(FacilityRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<PageDto<FacilityDto>>> ListAsync(FacilityListQuery query, string userId, CancellationToken cancellationToken);

    Task<OperationResult<FacilityDto>> GetAsync(Guid id, string userId, CancellationToken cancellationToken);

    Task<OperationResult<FacilityDto>> UpdateAsync(Guid id, FacilityRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<IReadOnlyList<LocationDto>>> ListLocationsAsync(Guid facilityId, string userId, CancellationToken cancellationToken);

    Task<OperationResult<LocationDto>> AddLocationAsync(Guid facilityId, LocationRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<MembershipDto>> AddMembershipAsync(Guid facilityId, MembershipRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);
}

public interface IRequisitionService
{
    Task<OperationResult<RequisitionDto>> CreateAsync(RequisitionRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<PageDto<RequisitionDto>>> ListAsync(RequisitionListQuery query, string userId, CancellationToken cancellationToken);

    Task<OperationResult<RequisitionDto>> GetAsync(Guid id, string userId, CancellationToken cancellationToken);

    Task<OperationResult<RequisitionDto>> UpdateAsync(Guid id, RequisitionRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<RequisitionDto>> ChangeStatusAsync(Guid id, RequisitionStatusRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<RequisitionDto>> AssignRecruiterAsync(Guid id, AssignRecruiterRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);
}
