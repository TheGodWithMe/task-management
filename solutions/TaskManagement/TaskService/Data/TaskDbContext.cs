using Microsoft.EntityFrameworkCore;
using TaskManagement.TaskService.Models;
using Task = TaskManagement.TaskService.Models.Task;
using TaskStatus = TaskManagement.TaskService.Models.TaskStatus;

namespace TaskManagement.TaskService.Data;

public class TaskDbContext : DbContext
{
    public TaskDbContext(DbContextOptions<TaskDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Task> Tasks => Set<Task>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TaskStatus> TaskStatuses => Set<TaskStatus>();
    public DbSet<TransitionRule> TransitionRules => Set<TransitionRule>();
    public DbSet<Participation> Participations => Set<Participation>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<TaskHistory> TaskHistories => Set<TaskHistory>();
    public DbSet<Priority> Priorities => Set<Priority>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
        });

        modelBuilder.Entity<Task>(entity =>
        {
            entity.ToTable("tasks");
            entity.Property<uint>("Version").IsRowVersion();
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(5000);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Priority).HasMaxLength(50).IsRequired();

            entity.HasOne(e => e.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Executor)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(e => e.ExecutorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Comments)
                .WithOne(c => c.Task)
                .HasForeignKey(c => c.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.History)
                .WithOne(h => h.Task)
                .HasForeignKey(h => h.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Login).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<TaskStatus>(entity =>
        {
            entity.ToTable("task_statuses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<TransitionRule>(entity =>
        {
            entity.ToTable("transition_rules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FromStatus).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ToStatus).HasMaxLength(50).IsRequired();
            entity.Property(e => e.UserRole).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<Participation>(entity =>
        {
            entity.ToTable("project_participants");
            entity.HasKey(e => new { e.ProjectId, e.ExecutorId });
            entity.Property(e => e.JoinedAt).IsRequired();

            entity.HasOne(e => e.Project)
                .WithMany(p => p.Participants)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Executor)
                .WithMany(u => u.Participations)
                .HasForeignKey(e => e.ExecutorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.ToTable("comments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).HasMaxLength(5000).IsRequired();

            entity.HasOne(e => e.Task)
                .WithMany(t => t.Comments)
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Author)
                .WithMany(u => u.Comments)
                .HasForeignKey(e => e.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskHistory>(entity =>
        {
            entity.ToTable("task_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FromStatus).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ToStatus).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Comment).HasMaxLength(5000).IsRequired();

            entity.HasOne(e => e.Task)
                .WithMany(t => t.History)
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Author)
                .WithMany(u => u.History)
                .HasForeignKey(e => e.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Priority>(entity =>
        {
            entity.ToTable("priorities");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
        });
    }
}
