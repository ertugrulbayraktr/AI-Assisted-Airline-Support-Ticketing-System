using Microsoft.EntityFrameworkCore;
using Support.Application.Interfaces;
using Support.Domain.Entities;

namespace Support.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<TicketAuditEvent> TicketAuditEvents => Set<TicketAuditEvent>();
    public DbSet<PolicyDocument> PolicyDocuments => Set<PolicyDocument>();
    public DbSet<PolicyChunk> PolicyChunks => Set<PolicyChunk>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
        });

        // Ticket
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TicketNumber).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.TicketNumber).IsUnique();
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.PNR).HasMaxLength(10);
            entity.Property(e => e.PassengerLastName).HasMaxLength(100);

            entity.HasMany(e => e.Messages)
                .WithOne(m => m.Ticket)
                .HasForeignKey(m => m.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.AuditEvents)
                .WithOne(a => a.Ticket)
                .HasForeignKey(a => a.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TicketMessage
        modelBuilder.Entity<TicketMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
        });

        // TicketAuditEvent
        modelBuilder.Entity<TicketAuditEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BeforeState).HasMaxLength(500);
            entity.Property(e => e.AfterState).HasMaxLength(500);
            entity.Property(e => e.Details).HasMaxLength(2000);
        });

        // PolicyDocument
        modelBuilder.Entity<PolicyDocument>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Content).IsRequired();

            entity.HasMany(e => e.Chunks)
                .WithOne(c => c.PolicyDocument)
                .HasForeignKey(c => c.PolicyDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PolicyChunk
        modelBuilder.Entity<PolicyChunk>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SectionTitle).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Content).IsRequired();
        });

        // Notification
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
        });
    }
}
