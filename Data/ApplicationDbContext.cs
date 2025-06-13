using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StickyFlow.Models;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
// At the top of ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore.Metadata.Builders;
// At the top of your files
using StickyFlow.Dtos;
using StickyFlow.Services;
using StickyFlow.Data;

namespace StickyFlow.Data
{
    public class ApplicationDbContext : IdentityDbContext<StickyFlow.Models.ApplicationUser>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IHttpContextAccessor httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // Main entities
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<Priority> Priorities => Set<Priority>();
        public DbSet<Status> Statuses => Set<Status>();
        public DbSet<ReminderLog> ReminderLogs => Set<ReminderLog>();
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            // Configure relationships and indexes
            builder.Entity<Note>()
            .HasOne(n => n.Priority)
            .WithMany()
            .HasForeignKey(n => n.PriorityId);

            builder.Entity<Note>()
                .HasOne(n => n.Status)
                .WithMany()
                .HasForeignKey(n => n.StatusId);

            builder.Entity<ReminderLog>()
                .HasOne(r => r.Note)
                .WithMany()
                .HasForeignKey(r => r.NoteId);

            builder.Entity<ReminderLog>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId);
            // Apply all configurations from assembly
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            // Configure relationships
            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Profile)
                .WithOne(p => p.User)
                .HasForeignKey<UserProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Note>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notes)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Global query filters
            builder.Entity<Note>().HasQueryFilter(n => !n.IsDeleted);
            builder.Entity<ApplicationUser>().HasQueryFilter(u => !u.IsDeleted);

            // Value conversions
            //builder.Entity<Priority>()
            //    .Property(p => p.Weight)
            //    .HasPrecision(5, 2);

            // JSON serialization for complex types
            builder.Entity<Note>()
                .OwnsOne(n => n.Metadata, nb =>
                {
                    nb.ToJson();
                    nb.Property(m => m.LastAccessed).HasColumnName("LastAccessed");
                });

            // Indexes
            builder.Entity<Note>()
                .HasIndex(n => new { n.UserId, n.DueDate });

            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.Email)
                .IsUnique();
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>()
                .HaveConversion<DateTimeToUtcConverter>();

            configurationBuilder.Properties<string>()
                .HaveMaxLength(200)
                .AreUnicode(false);
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            var auditEntries = OnBeforeSaveChanges();
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            await OnAfterSaveChanges(auditEntries);
            return result;
        }

        private List<AuditEntry> OnBeforeSaveChanges()
        {
            ChangeTracker.DetectChanges();
            var auditEntries = new List<AuditEntry>();
            var now = DateTime.UtcNow;
            var userId = _httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";

            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State is EntityState.Detached or EntityState.Unchanged)
                    continue;

                // Audit fields
                entry.Entity.LastModified = now;
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedById = userId;
                }
                entry.Entity.LastModifiedById = userId;

                // Audit logging
                var auditEntry = new AuditEntry(entry)
                {
                    Action = entry.State.ToString(),
                    UserId = userId,
                    Timestamp = now
                };
                auditEntries.Add(auditEntry);

                foreach (var property in entry.Properties)
                {
                    if (property.IsTemporary)
                    {
                        auditEntry.TemporaryProperties.Add(property);
                        continue;
                    }

                    if (property.IsModified)
                    {
                        auditEntry.ChangedColumns.Add(property.Metadata.Name);
                    }
                }
            }

            return auditEntries;
        }

        private async Task OnAfterSaveChanges(List<AuditEntry> auditEntries)
        {
            if (auditEntries == null || auditEntries.Count == 0)
                return;

            foreach (var auditEntry in auditEntries)
            {
                foreach (var prop in auditEntry.TemporaryProperties)
                {
                    if (prop.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                    else
                    {
                        auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                }

                AuditLogs.Add(auditEntry.ToAudit());
            }

            await base.SaveChangesAsync();
        }

        private class DateTimeToUtcConverter : ValueConverter<DateTime, DateTime>
        {
            public DateTimeToUtcConverter()
                : base(
                    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
            {
            }
        }
    }
    /*
    public class ApplicationUser : IdentityUser
    {
        // Extended properties
        public string FullName { get; set; }
        public DateTime MemberSince { get; set; } = DateTime.UtcNow;
        public string? RefreshToken { get; set; }
        public DateTime RefreshTokenExpiryTime { get; set; }
        public bool IsDeleted { get; set; }

        // Navigation properties
        public virtual UserProfile Profile { get; set; }
        public virtual ICollection<Note> Notes { get; set; }
        public virtual ICollection<ReminderLog> Reminders { get; set; }
    }
    */
    public class AuditEntry
    {
        public AuditEntry(EntityEntry entry)
        {
            Entry = entry;
        }

        public EntityEntry Entry { get; }
        public string Action { get; set; }
        public string UserId { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> KeyValues { get; } = new();
        public Dictionary<string, object> OldValues { get; } = new();
        public Dictionary<string, object> NewValues { get; } = new();
        public List<PropertyEntry> TemporaryProperties { get; } = new();
        public List<string> ChangedColumns { get; } = new();

        public bool HasTemporaryProperties => TemporaryProperties.Any();

        public AuditLog ToAudit()
        {
            return new AuditLog
            {
                Action = Action,
                Timestamp = Timestamp,
                UserId = UserId,
                EntityType = Entry.Metadata.ClrType.Name,
                KeyValues = JsonSerializer.Serialize(KeyValues),
                OldValues = OldValues.Count == 0 ? null : JsonSerializer.Serialize(OldValues),
                NewValues = NewValues.Count == 0 ? null : JsonSerializer.Serialize(NewValues),
                ChangedColumns = ChangedColumns.Count == 0 ? null : string.Join(",", ChangedColumns)
            };
        }
    }
}