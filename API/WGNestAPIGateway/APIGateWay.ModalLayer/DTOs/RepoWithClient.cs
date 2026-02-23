using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.PostData.PostHelper;

namespace APIGateWay.ModalLayer.DTOs
{
    public class RepoWithClient
    {
        public LoginMasterDto Login { get; set; }
        public ClientMasterDto? Client { get; set; }
        public PostRepositoryModel Repo { get; set; }
    }
    public class PostRepositoryModel : IAuditableUser, IAuditableEntity
    {
        [Key]
        public int SiNo { get; set; }
        public Guid? Repo_Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string RepoKey { get; set; }
        public string? Status { get; set; }
        public Guid? Owner1 { get; set; }
        public Guid? Owner2 { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
    public class PostRepoDto
    {
        [Key]
        public int SiNo { get; set; }

        public Guid? Repo_Id { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string? RepoKey { get; set; }

        public DateTime? CreatedAt { get; set; }

        public Guid? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public Guid? UpdatedBy { get; set; }

        public string? Status { get; set; }

        public Guid? Owner1 { get; set; }

        public Guid? Owner2 { get; set; }

        public List<RepoUserRegisterDto> userLists { get; set; }
        public TempReturn temp { get; set; }
    }
    public class RepoUserList
    : IAuditableUser, IAuditableEntity
    {
        [Key]
        public int? SiNo { get; set; }

        public Guid? UserId { get; set; }

        public string UserName { get; set; }

        public string PhoneNumber { get; set; }

        public string MailId { get; set; }

        public string Status { get; set; }

        public string RepoKey { get; set; }

        public DateTime? CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
    }

    public class RepoUserRegisterDto
    {
        [Key]
        public string UserName { get; set; }

        public string Password { get; set; }   // Only for register

        public string PhoneNumber { get; set; }

        public string MailId { get; set; }

        public int Role { get; set; }

        public Guid? UserId { get; set; }

        // public string RepoKey { get; set; }
    }


    public class RepoInsertResult
    {
        public PostRepositoryModel RepoEntity { get; set; }
        public List<RepoUserList> RepoUsers { get; set; }
        public ProcessedAttachmentResult AttachmentResult { get; set; }
    }

}
