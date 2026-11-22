using Microsoft.EntityFrameworkCore;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.Data;

public class ApplicationContext : DbContext
{
    public ApplicationContext(DbContextOptions<ApplicationContext> options) : base(options) { }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<EmailConfirmationToken> EmailConfirmationTokens { get; set; } = null!;
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; } = null!;
    public DbSet<Post> Posts { get; set; } = null!;
    public DbSet<Repost> Reposts { get; set; } = null!;
    public DbSet<Bookmark> Bookmarks { get; set; } = null!;
    public DbSet<Follow> Follows { get; set; } = null!;
    public DbSet<Conversation> Conversations { get; set; } = null!;
    public DbSet<Message> Messages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(builder =>
        {
            builder.HasIndex(u => u.Email).IsUnique();
            builder.Property(u => u.Username).HasMaxLength(User.MaxUsernameLength).IsRequired();
            builder.Property(u => u.NormalizedUsername).HasMaxLength(User.MaxUsernameLength).IsRequired();
            builder.HasIndex(u => u.NormalizedUsername).IsUnique();
        });

        modelBuilder.Entity<Follow>(builder =>
        {
            builder.HasKey(f => new { f.FollowerId, f.FollowingId });

            builder.HasOne(f => f.Follower)
                .WithMany(u => u.Following)
                .HasForeignKey(f => f.FollowerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(f => f.Following)
                .WithMany(u => u.Followers)
                .HasForeignKey(f => f.FollowingId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(f => new { f.FollowingId, f.CreatedAt });
            builder.HasIndex(f => new { f.FollowerId, f.CreatedAt });
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

        modelBuilder.Entity<Bookmark>(builder =>
        {
            builder.HasKey(b => new { b.PostId, b.UserId });

            builder.HasOne(b => b.Post)
                .WithMany(p => p.Bookmarks)
                .HasForeignKey(b => b.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(b => b.User)
                .WithMany(u => u.Bookmarks)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(b => new { b.UserId, b.CreatedAt });

            builder.HasQueryFilter(b => !b.Post.IsDeleted);
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

        modelBuilder.Entity<PasswordResetToken>(builder =>
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
                .WithMany(p => p.Replies)
                .HasForeignKey(p => p.ReplyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.Quote)
                .WithMany(p => p.Quotes)
                .HasForeignKey(p => p.QuoteId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(p => new { p.UserId, p.CreatedAt });
            builder.HasIndex(p => p.ReplyId);

            builder.HasIndex(p => p.QuoteId);

            builder.Property(p => p.Text).HasMaxLength(Post.MaxTextLength);

            builder.HasQueryFilter(p => !p.IsDeleted);
        });

        modelBuilder.Entity<Conversation>(builder =>
        {
            builder.ToTable(t =>
                t.HasCheckConstraint("CK_Conversations_ParticipantOrder", "\"ParticipantAId\" < \"ParticipantBId\""));

            builder.HasOne(c => c.ParticipantA)
                .WithMany()
                .HasForeignKey(c => c.ParticipantAId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(c => c.ParticipantB)
                .WithMany()
                .HasForeignKey(c => c.ParticipantBId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(c => new { c.ParticipantAId, c.ParticipantBId }).IsUnique();
            builder.HasIndex(c => c.ParticipantBId);
        });

        modelBuilder.Entity<Message>(builder =>
        {
            builder.HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(m => m.Text)
                .HasMaxLength(Message.MaxTextLength)
                .IsRequired();

            builder.HasIndex(m => new { m.ConversationId, m.SentAt, m.Id })
                .IsDescending(false, true, true);
        });
    }
}