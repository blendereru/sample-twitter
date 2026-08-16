using Microsoft.EntityFrameworkCore;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.Data;

public class ApplicationContext : DbContext
{
    public ApplicationContext(DbContextOptions<ApplicationContext> options) : base(options) { }
    
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<EmailConfirmationToken> EmailConfirmationTokens { get; set; } = null!;

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
    }
}