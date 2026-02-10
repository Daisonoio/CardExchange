using CardExchange.Core.Entities;

namespace CardExchange.Core.Interfaces
{
    public interface IEventRepository : IBaseRepository<Event>
    {
        Task<(IEnumerable<Event> Items, int TotalCount)> GetUpcomingEventsAsync(int page, int pageSize);
        Task<(IEnumerable<Event> Items, int TotalCount)> GetEventsByCityAsync(string city, int page, int pageSize);
        Task<IEnumerable<Event>> GetNearbyEventsAsync(decimal latitude, decimal longitude, int radiusKm, int limit = 20);
        Task<(IEnumerable<Event> Items, int TotalCount)> GetEventsByOrganizerAsync(int organizerId, int page, int pageSize);
        Task<Event?> GetEventWithParticipantsAsync(int eventId);
        Task<EventParticipant?> GetParticipantAsync(int eventId, int userId);
        Task<IEnumerable<Event>> GetUserEventsAsync(int userId);
        Task<int> GetParticipantCountAsync(int eventId);
    }
}
