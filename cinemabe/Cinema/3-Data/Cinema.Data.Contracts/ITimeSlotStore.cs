using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface ITimeSlotStore : IGenericStore<TimeSlot>
{
    /// <summary>True when the theater already has a time slot overlapping [startTime, endTime) (other than
    /// <paramref name="excludeTimeSlotId"/>). Times must be zero-padded "HH:mm".</summary>
    Task<bool> HasTimeSlotOverlapAsync(Guid theaterId, string startTime, string endTime, Guid? excludeTimeSlotId);
}
