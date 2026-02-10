using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.Infrastructure.Repositories
{
    public class EventRepository : BaseRepository<Event>, IEventRepository
    {
        public EventRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<(IEnumerable<Event> Items, int TotalCount)> GetUpcomingEventsAsync(int page, int pageSize)
        {
            var now = DateTime.UtcNow;
            var query = _dbSet
                .Where(e => e.Status == EventStatus.Published && e.StartDate > now)
                .Include(e => e.Organizer)
                .OrderBy(e => e.StartDate);

            var totalCount = await query.CountAsync();
            var items = await query
                .AsNoTracking()
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(IEnumerable<Event> Items, int TotalCount)> GetEventsByCityAsync(string city, int page, int pageSize)
        {
            var now = DateTime.UtcNow;
            var query = _dbSet
                .Where(e => e.Status == EventStatus.Published
                    && e.StartDate > now
                    && e.City.ToLower() == city.ToLower())
                .Include(e => e.Organizer)
                .OrderBy(e => e.StartDate);

            var totalCount = await query.CountAsync();
            var items = await query
                .AsNoTracking()
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<IEnumerable<Event>> GetNearbyEventsAsync(decimal latitude, decimal longitude, int radiusKm, int limit = 20)
        {
            var now = DateTime.UtcNow;

            // Haversine in SQL
            var events = await _dbSet
                .Where(e => e.Status == EventStatus.Published
                    && e.StartDate > now
                    && e.Latitude != null
                    && e.Longitude != null)
                .Include(e => e.Organizer)
                .AsNoTracking()
                .ToListAsync();

            // Calcolo distanza in-memory (Haversine)
            var lat1Rad = (double)latitude * Math.PI / 180.0;

            return events
                .Select(e =>
                {
                    var lat2Rad = (double)e.Latitude!.Value * Math.PI / 180.0;
                    var dLat = lat2Rad - lat1Rad;
                    var dLon = ((double)e.Longitude!.Value - (double)longitude) * Math.PI / 180.0;
                    var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                          + Math.Cos(lat1Rad) * Math.Cos(lat2Rad)
                          * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
                    var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
                    var distanceKm = 6371.0 * c;
                    return new { Event = e, Distance = distanceKm };
                })
                .Where(x => x.Distance <= radiusKm)
                .OrderBy(x => x.Distance)
                .Take(limit)
                .Select(x => x.Event)
                .ToList();
        }

        public async Task<(IEnumerable<Event> Items, int TotalCount)> GetEventsByOrganizerAsync(int organizerId, int page, int pageSize)
        {
            var query = _dbSet
                .Where(e => e.OrganizerId == organizerId)
                .OrderByDescending(e => e.StartDate);

            var totalCount = await query.CountAsync();
            var items = await query
                .AsNoTracking()
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Event?> GetEventWithParticipantsAsync(int eventId)
        {
            return await _dbSet
                .Include(e => e.Organizer)
                .Include(e => e.Participants)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(e => e.Id == eventId);
        }

        public async Task<EventParticipant?> GetParticipantAsync(int eventId, int userId)
        {
            return await _context.Set<EventParticipant>()
                .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == userId);
        }

        public async Task<IEnumerable<Event>> GetUserEventsAsync(int userId)
        {
            var now = DateTime.UtcNow;
            var eventIds = await _context.Set<EventParticipant>()
                .Where(ep => ep.UserId == userId && ep.Status != ParticipationStatus.Cancelled)
                .Select(ep => ep.EventId)
                .ToListAsync();

            return await _dbSet
                .Where(e => eventIds.Contains(e.Id) && e.StartDate > now)
                .Include(e => e.Organizer)
                .OrderBy(e => e.StartDate)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetParticipantCountAsync(int eventId)
        {
            return await _context.Set<EventParticipant>()
                .CountAsync(ep => ep.EventId == eventId && ep.Status != ParticipationStatus.Cancelled);
        }
    }
}
