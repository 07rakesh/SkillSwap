using Microsoft.EntityFrameworkCore;
using SkillSwapAI.Models;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Skill> Skills { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<SkillRequest> SkillRequests { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<MentorAvailability> MentorAvailabilities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Skill>()
            .HasOne(s => s.User)
            .WithMany(u => u.Skills)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SkillRequest>()
            .HasOne(sr => sr.Skill)
            .WithMany()
            .HasForeignKey(sr => sr.SkillId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SkillRequest>()
            .HasOne(sr => sr.RequesterUser)
            .WithMany()
            .HasForeignKey(sr => sr.RequesterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SkillRequest>()
            .HasOne(sr => sr.OwnerUser)
            .WithMany()
            .HasForeignKey(sr => sr.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Session>()
            .HasOne(s => s.SkillRequest)
            .WithMany(sr => sr.Sessions)
            .HasForeignKey(s => s.SkillRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Receiver)
            .WithMany()
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.Session)
            .WithMany(s => s.Reviews)
            .HasForeignKey(r => r.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.Reviewer)
            .WithMany()
            .HasForeignKey(r => r.ReviewerId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.Teacher)
            .WithMany()
            .HasForeignKey(r => r.TeacherId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}