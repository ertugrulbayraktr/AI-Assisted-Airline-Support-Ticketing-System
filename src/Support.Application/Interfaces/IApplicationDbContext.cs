using Microsoft.EntityFrameworkCore;
using Support.Domain.Entities;

namespace Support.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<TicketMessage> TicketMessages { get; }
    DbSet<TicketAuditEvent> TicketAuditEvents { get; }
    DbSet<PolicyDocument> PolicyDocuments { get; }
    DbSet<PolicyChunk> PolicyChunks { get; }
    DbSet<Notification> Notifications { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
