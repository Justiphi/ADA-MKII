using ADA_MKII_Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADA_MKII_Data;

/// <summary>
/// The single EF Core context, backed by SQL Server on the VPS. This type is
/// reachable only from ADA-MKII-Server: no client head may reference this
/// assembly, so no device ever holds a database credential.
/// </summary>
public sealed class AdaDbContext(DbContextOptions<AdaDbContext> options) : DbContext(options)
{
    public DbSet<ConversationEntity> Conversations => Set<ConversationEntity>();

    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    public DbSet<SettingEntity> Settings => Set<SettingEntity>();

    public DbSet<DeviceTokenEntity> DeviceTokens => Set<DeviceTokenEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ConversationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.UpdatedUtc).IsDescending();

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
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(100);
            entity.Property(e => e.Value).HasMaxLength(4000).IsRequired();
        });

        modelBuilder.Entity<DeviceTokenEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            // SHA-256 as lowercase hex is always 64 chars.
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.Name).IsUnique();
        });
    }
}
