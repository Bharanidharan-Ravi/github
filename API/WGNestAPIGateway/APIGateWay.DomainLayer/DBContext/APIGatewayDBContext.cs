using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using static APIGateWay.ModalLayer.Helper.HelperModal;
using static APIGateWay.ModalLayer.PostData.PostHelper;

namespace APIGateWay.DomainLayer.DBContext
{
    public class APIGatewayDBContext : DbContext
    {
        private readonly ILoginContextService _loginContext;
        private readonly IConfiguration _configuration;

        public APIGatewayDBContext(DbContextOptions<APIGatewayDBContext> options, ILoginContextService loginContext,
            IConfiguration configuration) : base(options)
        {
            _loginContext = loginContext;
            _configuration = configuration;
        }


        #region OnConfiguring (Dynamic DB)

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var baseConnString =
                    _configuration.GetConnectionString("DefaultConnection");

                var dynamicDBName = _loginContext.databaseName;

                if (!string.IsNullOrEmpty(dynamicDBName))
                {
                    var connectionBuilder =
                        new SqlConnectionStringBuilder(baseConnString);

                    connectionBuilder.InitialCatalog = dynamicDBName;

                    optionsBuilder.UseSqlServer(
                        connectionBuilder.ConnectionString
                    );
                }
                else
                {
                    optionsBuilder.UseSqlServer(baseConnString);
                }
            }
        }

        #endregion
        //public DbSet<LOGIN_MASTER> lOGIN_MASTER { get; set; }
        public DbSet<GetUserModel> getUserModels { get; set; }  
        public DbSet<LOGIN_MASTER> LOGIN_MASTER { get; set; }
        public DbSet<EMPLOYEEMASTER> eMPLOYEEMASTERs { get; set; }
        public DbSet<ClientMaster> clientMasters { get; set; }
        public DbSet<GetUserforValidate> getUserforValidates { get; set; }
        public DbSet<CLIENTSMAILIDS> cLIENTSMAILIDs { get; set; }
        public DbSet<GetEmployee> getEmployees { get; set; }
        public DbSet<GetProject> getProjects { get; set; }
        public DbSet<GetRepo> getRepos { get; set; }
        public DbSet<GetTickets> getTickets { get; set; }
        public DbSet<LabelMaster> labelMaster { get; set; }
        public DbSet<PostRepositoryModel> RepositoryMasters { get; set; }
        public DbSet<RepoUserList> RepoUsers { get; set; }
        public DbSet<SequenceResult> sequenceResults { get; set; }
        public DbSet<AttachmentMaster> AttachmentMaster { get; set; }


        #region SaveChanges Override (Audit)

        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default
        )
        {
            var currentUserId = _loginContext.userId;

            var entries = ChangeTracker
                .Entries()
                .Where(e =>
                    e.Entity is IAuditableEntity &&
                    (e.State == EntityState.Added ||
                     e.State == EntityState.Modified)
                );

            var indiaTimeZone =
                TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

            var indiaTime =
                TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    indiaTimeZone
                );

            foreach (var entry in entries)
            {
                // DATE AUDIT
                if (entry.Entity is IAuditableEntity auditableDate)
                {
                    if (entry.State == EntityState.Added)
                    {
                        auditableDate.CreatedAt = indiaTime;
                        auditableDate.UpdatedAt =
                            auditableDate.CreatedAt;
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        auditableDate.UpdatedAt = indiaTime;

                        entry.Property(nameof(IAuditableEntity.CreatedAt))
                             .IsModified = false;
                    }
                }

                // USER AUDIT
                if (entry.Entity is IAuditableUser auditableUser)
                {
                    if (entry.State == EntityState.Added)
                    {
                        auditableUser.CreatedBy = currentUserId;
                        auditableUser.UpdatedBy = currentUserId;
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        auditableUser.UpdatedBy = currentUserId;

                        entry.Property("CreatedBy")
                             .IsModified = false;
                    }
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }

        #endregion

        #region Model Creating

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LOGIN_MASTER>(entity =>
            {
                entity.Property(e => e.Status).HasDefaultValue("Active");
            });
            modelBuilder.Entity<EMPLOYEEMASTER>()
                .HasOne(e => e.Login)
                .WithOne() // 👈 No navigation on LOGIN_MASTER side
                .HasForeignKey<EMPLOYEEMASTER>(e => e.EmployeeID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EMPLOYEEMASTER>(entity =>
            {
                entity.Property(e => e.Status).HasDefaultValue("Active");
                entity.Property(e => e.CreatedOn).HasDefaultValueSql("GETDATE()");
            });
            //modelBuilder.Entity<CLIENTSMAILIDS>()
            //    .HasNoKey();

            modelBuilder.Entity<GetEmployee>().HasNoKey();
            modelBuilder.Entity<ClientMaster>()
                .Property(c => c.Created_On).HasColumnType("datetime");

            modelBuilder.Entity<ClientMaster>()
                .Property(c => c.Updated_On).HasColumnType("datetime");

            modelBuilder.Entity<ClientMaster>()
                .Property(c => c.Valid_From).HasColumnType("datetime");

            modelBuilder.Entity<ClientMaster>()
                .Property(c => c.Valid_To).HasColumnType("datetime");
        }
        #endregion
    }
}
