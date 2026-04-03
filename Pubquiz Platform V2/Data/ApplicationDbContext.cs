using Microsoft.EntityFrameworkCore;
using Pubquiz_Platform.Data.Entities;

namespace Pubquiz_Platform.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Quiz> Quizzes { get; set; }
        public DbSet<Question> Questions { get; set; }

        public DbSet<Lobby> Lobbies { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Relatie: User → Quizzes (1:N)
            modelBuilder.Entity<Quiz>()
                .HasOne(q => q.QuizMaster)
                .WithMany(u => u.Quizzes)
                .HasForeignKey(q => q.QuizMasterId);

            // Relatie: Quiz → Questions (1:N) with cascade delete
            modelBuilder.Entity<Question>()
                .HasOne(q => q.Quiz)
                .WithMany(z => z.Questions)
                .HasForeignKey(q => q.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Quiz>()
            .HasIndex(q => new { q.QuizMasterId, q.QuizSlug })
            .IsUnique();
        }
    }
}
