using Microsoft.EntityFrameworkCore;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.Data;

public class ApplicationContext : DbContext
{
    public ApplicationContext(DbContextOptions<ApplicationContext> options) : base(options) { }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<EmailConfirmationToken> EmailConfirmationTokens { get; set; } = null!;
    public DbSet<Post> Posts { get; set; } = null!;
    public DbSet<Repost> Reposts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(builder =>
        {
            builder.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Repost>(builder =>
        {
            builder.HasKey(r => new { r.PostId, r.UserId });

            builder.HasOne(r => r.Post)
                .WithMany(p => p.Reposts)
                .HasForeignKey(r => r.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(r => r.User)
                .WithMany(u => u.Reposts)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(r => new { r.UserId, r.CreatedAt });

            builder.HasQueryFilter(r => !r.Post.IsDeleted);
        });

        modelBuilder.Entity<EmailConfirmationToken>(builder =>
        {
            builder.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(t => t.TokenHash).IsUnique();
            builder.HasIndex(t => new { t.UserId, t.UsedAt });
        });

        modelBuilder.Entity<Post>(builder =>
        {
            builder.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(p => p.Reply)
                .WithMany()
                .HasForeignKey(p => p.ReplyId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(p => new { p.UserId, p.CreatedAt });
            builder.HasIndex(p => p.ReplyId);

            builder.Property(p => p.Text).HasMaxLength(280);

            builder.HasQueryFilter(p => !p.IsDeleted);
        });
    }
}