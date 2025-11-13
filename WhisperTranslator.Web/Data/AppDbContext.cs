using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WhisperTranslator.Web.Models;

namespace WhisperTranslator.Web.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Conversation> Conversations => Set<Conversation>();
        public DbSet<Message> Messages => Set<Message>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            // IMPORTANT: let Identity configure its keys first
            base.OnModelCreating(b);

            // ---- Explicit table names for Identity (keeps Identity composite keys intact) ----
            b.Entity<ApplicationUser>().ToTable("wisper_users");
            b.Entity<IdentityRole>().ToTable("wisper_roles");
            b.Entity<IdentityUserRole<string>>().ToTable("wisper_user_roles");
            b.Entity<IdentityUserClaim<string>>().ToTable("wisper_user_claims");
            b.Entity<IdentityUserLogin<string>>().ToTable("wisper_user_logins");
            b.Entity<IdentityRoleClaim<string>>().ToTable("wisper_role_claims");
            b.Entity<IdentityUserToken<string>>().ToTable("wisper_user_tokens");

            // ---- Your domain tables ----
            b.Entity<Conversation>(e =>
            {
                e.ToTable("wisper_conversations");
                e.HasKey(x => x.Id);
                e.Property(x => x.CreatedUtc).HasDefaultValueSql("SYSUTCDATETIME()");
                e.Property(x => x.UniqueRef).HasMaxLength(64);
                e.HasIndex(x => x.UniqueRef).IsUnique().HasFilter("[UniqueRef] IS NOT NULL");
            });
            b.Entity<Message>().ToTable("wisper_messages");

            b.Entity<Conversation>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.CreatedUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            });

            b.Entity<Message>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasOne(x => x.Conversation)
                    .WithMany(c => c.Messages)
                    .HasForeignKey(x => x.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.Property(x => x.TimestampUtc).HasDefaultValueSql("SYSUTCDATETIME()");
                e.Property(x => x.Role).HasMaxLength(16);
                e.Property(x => x.SourceLang).HasMaxLength(10);
                e.Property(x => x.TargetLang).HasMaxLength(10);
            });
        }
    }
}
