using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WGAPP.ModelLayer.GithubModal.ProjectModal
{
    public class ProjectMaster
    {
        public Guid? Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime? Created_On { get; set; }
        public Guid? Created_By { get; set; }
        public DateTime? Updated_On { get; set; }
        public Guid? Updated_By { get; set; }
        public Guid? Client_Id { get; set; }
        public string? Status { get; set; }
        public Guid? Repo_Id { get; set; }
        public string ProjCode { get; set; }
        public Guid? Responsible { get; set; }
        public DateTime DueDate { get; set; }
    }

}
