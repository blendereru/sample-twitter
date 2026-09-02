using Microsoft.EntityFrameworkCore;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.Data;

public class ApplicationContext : DbContext
{
    public ApplicationContext(DbContextOptions<ApplicationContext> options) : base(options) { }
    
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<EmailConfirmationToken> EmailConfirmationTokens { get; set; } = null!;
    public DbSet<Post> Posts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(builder =>
        {
            builder.HasIndex(u => u.Email).IsUnique();
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
        });
    }
}