using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using APIGateWay.ModalLayer;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using Microsoft.EntityFrameworkCore;

namespace APIGateWay.DomainLayer.DBContext
{
    public class APIGatewayDBContext : DbContext
    {
        public APIGatewayDBContext(DbContextOptions<APIGatewayDBContext> options) : base(options)
        {
        }

        //public DbSet<LOGIN_MASTER> lOGIN_MASTER { get; set; }
        public DbSet<GetUserModel> getUserModels { get; set; }  
        public DbSet<LOGIN_MASTER> lOGIN_MASTER { get; set; }
        public DbSet<EMPLOYEEMASTER> eMPLOYEEMASTERs { get; set; }
        public DbSet<ClientMaster> clientMasters { get; set; }
        public DbSet<GetUserforValidate> getUserforValidates { get; set; }
        public DbSet<CLIENTSMAILIDS> cLIENTSMAILIDs { get; set; }
        public DbSet<GetEmployee> getEmployees { get; set; }
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
    }
}
