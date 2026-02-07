using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WGAPP.ModelLayer.GithubModal.RepositoryModal
{
    public class PostRepositoryModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid? Repo_Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Repo_Code { get; set; }
        public DateTime? Created_On { get; set; }
        public Guid? Created_By { get; set; }
        public DateTime? Updated_On { get; set; }
        public string? Status { get; set; }
        public Guid? Owner1 { get; set; }
        public Guid? Owner2 { get; set; }
        public Guid? Updated_By { get; set; }
        public Guid? Client_Id { get; set; }
        //public string? DBname { get; set; }
    }
}
