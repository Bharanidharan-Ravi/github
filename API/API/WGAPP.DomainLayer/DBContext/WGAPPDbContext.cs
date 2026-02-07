using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Data.SqlClient;
using WGAPP.ModelLayer;
using WGAPP.ModelLayer.GithubModal.TicketingModal;
using WGAPP.ModelLayer.GithubModal.RepositoryModal;
using WGAPP.ModelLayer.GithubModal.MasterData;
using WGAPP.ModelLayer.GithubModal.ProjectModal;
using WGAPP.ModelLayer.GithubModal.ViewIssues;

namespace WGAPP.DomainLayer.DBContext
{
    public class WGAPPDbContext : DbContext
    {
        public WGAPPDbContext(DbContextOptions<WGAPPDbContext> options) : base(options)
        {
        }
        public DbSet<GetUserModel> getUserModels {  get; set; }
        public DbSet<GetThreadModal> getIssuesModals {  get; set; }
        public DbSet<RepoData> GetRepoDatas {  get; set; }
        public DbSet<PostRepositoryModel> REPOSITORIES { get; set; }
        public DbSet<IssueMaster> IssueMasters { get; set; }
        public DbSet<AttachmentMaster> AttachmentMasters { get; set; }
        //public DbSet<IssueAttachment> IssueAttachments { get; set; }
        public DbSet<GetLabelMaster> GetLabelMasters { get; set; }
        public DbSet<GetAllIssueData> getAllIssueDatas { get; set; }
        public DbSet<GetThreadModal> getIssueThread { get; set; }
        public DbSet<ProjectMaster> PROJECTMASTER { get; set; }
        public DbSet<GetProject> getProjects { get; set; }
        public DbSet<LabelMaster> labelMasters { get; set; }
        public DbSet<ISSUE_LABELS> Labels { get; set; }
        public DbSet<SequenceResult> sequenceResult { get; set; }
        public DbSet<IssuesThread> ISSUETHREADS { get; set; }

        #region masterData
        public DbSet<ClientsMasterData> clientMasters { get; set; }
        #endregion

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<PostRepositoryModel>()
             .HasKey(x => x.Repo_Id);
            modelBuilder.Entity<PostRepositoryModel>()
                .Property(x => x.Repo_Id)
                .HasDefaultValueSql("NEWID()");
            //  modelBuilder.Entity<PostRepositoryModel>()
            //.Property(r => r.Repo_Id)
            //.HasDefaultValueSql("NEWID()");
            // Table Mapping
            modelBuilder.Entity<IssueMaster>().ToTable("ISSUEMASTER");
            modelBuilder.Entity<AttachmentMaster>().ToTable("ATTACHMENTMASTER");
            modelBuilder.Entity<ISSUE_LABELS>().ToTable("ISSUE_LABELS");
            modelBuilder.Entity<ProjectMaster>().ToTable("PROJECTMASTER");
            modelBuilder.Entity<GetThreadModal>().HasNoKey();
            modelBuilder.Entity<GetUserModel>().HasNoKey();

            //// Composite Key
            //modelBuilder.Entity<IssueAttachment>()
            //    .HasKey(ia => new { ia.Issue_Id, ia.Attachment_Id });

            //modelBuilder.Entity<IssueAttachment>()
            //    .HasOne(ia => ia.Attachment)
            //    .WithMany(a => a.IssueAttachments)
            //    .HasForeignKey(ia => ia.Attachment_Id)
            //    .OnDelete(DeleteBehavior.Cascade);
           
         
        }
    }
}
