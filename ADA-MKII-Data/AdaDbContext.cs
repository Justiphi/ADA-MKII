using ADA_MKII_Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADA_MKII_Data;

/// <summary>
/// The single EF Core context, backed by SQL Server. Reachable from
/// ADA-MKII-Server and ADA-MKII-DataManager only: no client head may reference
/// this assembly, so no phone or browser ever holds a database credential.
/// </summary>
public sealed class AdaDbContext(DbContextOptions<AdaDbContext> options) : DbContext(options)
{
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();

    public DbSet<ConversationEntity> Conversations => Set<ConversationEntity>();

    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    public DbSet<SettingEntity> Settings => Set<SettingEntity>();

    public DbSet<DeviceTokenEntity> DeviceTokens => Set<DeviceTokenEntity>();

    public DbSet<NoteEntity> Notes => Set<NoteEntity>();

    public DbSet<MemoryEntity> Memories => Set<MemoryEntity>();

    public DbSet<CalendarEventEntity> CalendarEvents => Set<CalendarEventEntity>();

    public DbSet<CalendarExceptionEntity> CalendarExceptions => Set<CalendarExceptionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AccountEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).HasMaxLength(64).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(128).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
        });

        modelBuilder.Entity<ConversationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();

            // Composite index: every conversation query filters by owner first.
            entity.HasIndex(e => new { e.AccountId, e.UpdatedUtc }).IsDescending(false, true);

            entity.HasOne(e => e.Account)
                .WithMany(a => a.Conversations)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Messages)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MessageEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Role).HasConversion<int>();
            entity.HasIndex(e => new { e.ConversationId, e.CreatedUtc });
        });

        modelBuilder.Entity<SettingEntity>(entity =>
        {
            entity.HasKey(e => new { e.AccountId, e.Key });
            entity.Property(e => e.Key).HasMaxLength(100);
            entity.Property(e => e.Value).HasMaxLength(4000).IsRequired();

            entity.HasOne(e => e.Account)
                .WithMany(a => a.Settings)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NoteEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Content).IsRequired();

            // Every note query filters by owner first, then orders by recency.
            entity.HasIndex(e => new { e.AccountId, e.UpdatedUtc }).IsDescending(false, true);

            entity.HasOne(e => e.Account)
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MemoryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Tag).HasMaxLength(64);
            entity.HasIndex(e => new { e.AccountId, e.CreatedUtc }).IsDescending(false, true);

            entity.HasOne(e => e.Account)
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CalendarEventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Location).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(4000);
            entity.Property(e => e.Frequency).HasConversion<int>();

            // Long enough for any IANA or Windows zone id; the longest in the
            // Windows list is comfortably under 100 characters.
            entity.Property(e => e.TimeZoneId).HasMaxLength(100);

            // Range queries load every series that could contribute an occurrence,
            // so the useful index is owner plus series start.
            entity.HasIndex(e => new { e.AccountId, e.StartsUtc });

            entity.HasOne(e => e.Account)
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Exceptions)
                .WithOne(x => x.Event)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CalendarExceptionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            // One cancellation per occurrence; cancelling twice is not a thing.
            entity.HasIndex(e => new { e.EventId, e.OccurrenceStartUtc }).IsUnique();
        });

        modelBuilder.Entity<DeviceTokenEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            // SHA-256 as lowercase hex is always 64 chars.
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.AccountId);

            entity.HasOne(e => e.Account)
                .WithMany(a => a.DeviceTokens)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
