using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartStaff.Domain.Notifications;
using SmartStaff.Domain.Recruitment;
using SmartStaff.Domain.Scheduling;
using SmartStaff.Domain.Staffing;
using SmartStaff.Domain.Timekeeping;
using SmartStaff.Infrastructure.Persistence;

namespace SmartStaff.Infrastructure.Notifications;

public sealed class NotificationOutboxInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is ApplicationDbContext db)
        {
            Enqueue(db);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is ApplicationDbContext db)
        {
            Enqueue(db);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Enqueue(ApplicationDbContext db)
    {
        var pending = new List<NotificationOutbox>();
        foreach (var entry in db.ChangeTracker.Entries<JobApplication>().Where(item => item.State == EntityState.Added))
        {
            var requisition = Find(db.JobRequisitions, entry.Entity.JobRequisitionId);
            if (requisition is null)
            {
                continue;
            }
            foreach (var userId in Staff(db, requisition.FacilityId, requisition.AssignedRecruiterId))
            {
                pending.Add(Message(userId, "ApplicationReceived", "New clinician application", "A clinician applied to a requisition.", "JobApplication", entry.Entity.Id, $"ApplicationReceived:{entry.Entity.Id}:{userId}"));
            }
        }

        foreach (var entry in db.ChangeTracker.Entries<ApplicationStatusHistory>().Where(item => item.State == EntityState.Added))
        {
            var application = Find(db.JobApplications, entry.Entity.JobApplicationId);
            if (application is null)
            {
                continue;
            }

            var profile = Find(db.ClinicianProfiles, application.ClinicianProfileId);
            var requisition = Find(db.JobRequisitions, application.JobRequisitionId);
            if (profile is null || requisition is null)
            {
                continue;
            }
            var type = entry.Entity.NewStatus switch
            {
                ApplicationStatus.Interview => "InterviewScheduled",
                ApplicationStatus.Offer => "OfferCreated",
                _ => "ApplicationStatusChanged"
            };
            var title = entry.Entity.NewStatus switch
            {
                ApplicationStatus.Interview => "Interview scheduled",
                ApplicationStatus.Offer => "Offer created",
                _ => "Application updated"
            };
            foreach (var userId in new[] { profile.UserId }.Concat(Staff(db, requisition.FacilityId, requisition.AssignedRecruiterId)).Distinct())
            {
                pending.Add(Message(userId, type, title, $"The application is now {entry.Entity.NewStatus}.", "JobApplication", application.Id, $"{type}:{entry.Entity.Id}:{userId}"));
            }
        }

        foreach (var entry in db.ChangeTracker.Entries<Placement>().Where(item => item.State == EntityState.Added))
        {
            var profile = Find(db.ClinicianProfiles, entry.Entity.ClinicianProfileId);
            if (profile is null)
            {
                continue;
            }

            foreach (var userId in new[] { profile.UserId }.Concat(Managers(db, entry.Entity.FacilityId)).Distinct())
            {
                pending.Add(Message(userId, "PlacementConfirmed", "Placement confirmed", "A placement is now active.", "Placement", entry.Entity.Id, $"PlacementConfirmed:{entry.Entity.Id}:{userId}"));
            }
        }

        foreach (var entry in db.ChangeTracker.Entries<Shift>().Where(item => item.State == EntityState.Modified && item.Entity.Status == ShiftScheduleStatus.Published))
        {
            var original = entry.OriginalValues.GetValue<ShiftScheduleStatus>(nameof(Shift.Status));
            if (original == ShiftScheduleStatus.Published)
            {
                continue;
            }

            foreach (var userId in PlacedClinicians(db, entry.Entity.FacilityId))
            {
                pending.Add(Message(userId, "ShiftPublished", "New shift published", $"{entry.Entity.Specialty} was published for your facility.", "Shift", entry.Entity.Id, $"ShiftPublished:{entry.Entity.Id}:{userId}"));
            }
        }

        foreach (var entry in db.ChangeTracker.Entries<ShiftAssignment>().Where(item => item.State == EntityState.Added))
        {
            var profile = Find(db.ClinicianProfiles, entry.Entity.ClinicianProfileId);
            if (profile is null)
            {
                continue;
            }

            pending.Add(Message(profile.UserId, "ShiftAssigned", "Shift assigned", "You were assigned to a shift.", "ShiftAssignment", entry.Entity.Id, $"ShiftAssigned:{entry.Entity.Id}"));
        }

        foreach (var entry in db.ChangeTracker.Entries<ShiftAssignment>().Where(item => item.State == EntityState.Modified && item.Entity.AssignmentStatus == AssignmentStatus.Confirmed))
        {
            var original = entry.OriginalValues.GetValue<AssignmentStatus>(nameof(ShiftAssignment.AssignmentStatus));
            if (original == AssignmentStatus.Confirmed)
            {
                continue;
            }

            var shift = Find(db.Shifts, entry.Entity.ShiftId);
            if (shift is null)
            {
                continue;
            }
            foreach (var userId in Managers(db, shift.FacilityId))
            {
                pending.Add(Message(userId, "ShiftConfirmed", "Shift confirmed", "A clinician confirmed a shift assignment.", "ShiftAssignment", entry.Entity.Id, $"ShiftConfirmed:{entry.Entity.Id}:{userId}"));
            }
        }

        foreach (var entry in db.ChangeTracker.Entries<ShiftAssignment>().Where(item => item.State == EntityState.Modified && item.Entity.AssignmentStatus == AssignmentStatus.Cancelled))
        {
            var profile = Find(db.ClinicianProfiles, entry.Entity.ClinicianProfileId);
            if (profile is null)
            {
                continue;
            }

            pending.Add(Message(profile.UserId, "ShiftCancelled", "Shift assignment cancelled", "A shift assignment was cancelled.", "ShiftAssignment", entry.Entity.Id, $"ShiftAssignmentCancelled:{entry.Entity.Id}"));
        }

        foreach (var entry in db.ChangeTracker.Entries<Shift>().Where(item => item.State == EntityState.Modified && item.Entity.Status == ShiftScheduleStatus.Cancelled))
        {
            var assignments = db.ShiftAssignments.AsNoTracking().Where(item => item.ShiftId == entry.Entity.Id && item.AssignmentStatus != AssignmentStatus.Cancelled).Select(item => item.ClinicianProfileId).ToList();
            var users = db.ClinicianProfiles.AsNoTracking().Where(item => assignments.Contains(item.Id)).Select(item => item.UserId).ToList();
            foreach (var userId in users)
            {
                pending.Add(Message(userId, "ShiftCancelled", "Shift cancelled", "A scheduled shift was cancelled.", "Shift", entry.Entity.Id, $"ShiftCancelled:{entry.Entity.Id}:{userId}"));
            }
        }

        foreach (var entry in db.ChangeTracker.Entries<TimesheetApprovalHistory>().Where(item => item.State == EntityState.Added && item.Entity.NewStatus != item.Entity.PreviousStatus))
        {
            var timesheet = Find(db.Timesheets, entry.Entity.TimesheetId);
            if (timesheet is null)
            {
                continue;
            }

            var profile = Find(db.ClinicianProfiles, timesheet.ClinicianProfileId);
            if (profile is null)
            {
                continue;
            }
            if (entry.Entity.NewStatus == TimesheetStatus.Submitted)
            {
                foreach (var userId in Managers(db, timesheet.FacilityId))
                {
                    pending.Add(Message(userId, "TimesheetSubmitted", "Timesheet submitted", "A timesheet is waiting for review.", "Timesheet", timesheet.Id, $"TimesheetSubmitted:{entry.Entity.Id}:{userId}"));
                }
            }
            else if (entry.Entity.NewStatus is TimesheetStatus.Approved or TimesheetStatus.Rejected)
            {
                var approved = entry.Entity.NewStatus == TimesheetStatus.Approved;
                pending.Add(Message(profile.UserId, approved ? "TimesheetApproved" : "TimesheetRejected", approved ? "Timesheet approved" : "Timesheet rejected", approved ? "Your timesheet was approved." : "Your timesheet was rejected.", "Timesheet", timesheet.Id, $"TimesheetReviewed:{entry.Entity.Id}"));
            }
        }

        foreach (var message in pending.GroupBy(item => item.IdempotencyKey).Select(group => group.First()))
        {
            if (db.NotificationOutbox.Local.All(item => item.IdempotencyKey != message.IdempotencyKey))
            {
                db.NotificationOutbox.Add(message);
            }
        }
    }

    private static TEntity? Find<TEntity>(DbSet<TEntity> set, Guid id) where TEntity : class
    {
        foreach (var item in set.Local)
        {
            if (item?.GetType().GetProperty("Id")?.GetValue(item) is Guid found && found == id)
            {
                return item;
            }
        }

        return set.AsNoTracking().FirstOrDefault(item => EF.Property<Guid>(item, "Id") == id);
    }

    private static IEnumerable<string> Managers(ApplicationDbContext db, Guid facilityId) =>
        db.FacilityMemberships.AsNoTracking().Where(item => item.FacilityId == facilityId && item.MembershipRole == MembershipRole.Manager).Select(item => item.UserId).ToList();

    private static IEnumerable<string> Staff(ApplicationDbContext db, Guid facilityId, string? recruiterId)
    {
        var users = Managers(db, facilityId).ToList();
        if (!string.IsNullOrWhiteSpace(recruiterId))
        {
            users.Add(recruiterId);
        }

        return users.Distinct();
    }

    private static List<string> PlacedClinicians(ApplicationDbContext db, Guid facilityId) =>
        (from placement in db.Placements.AsNoTracking()
         join profile in db.ClinicianProfiles.AsNoTracking() on placement.ClinicianProfileId equals profile.Id
         where placement.FacilityId == facilityId && placement.Status == PlacementStatus.Active
         select profile.UserId).Distinct().ToList();

    private static NotificationOutbox Message(string userId, string type, string title, string message, string entityType, Guid entityId, string key) => new()
    {
        Id = Guid.NewGuid(),
        IdempotencyKey = key,
        RecipientUserId = userId,
        NotificationType = type,
        Title = title,
        Message = message,
        RelatedEntityType = entityType,
        RelatedEntityId = entityId
    };
}
