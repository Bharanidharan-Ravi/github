using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WGAPP.ModelLayer.GithubModal.RepositoryModal
{
    public class RepoData
    {
        [Key]
        public Guid Repo_Id { get; set; }
        public string? Repo_Code { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateTime? Created_On { get; set; }
        public Guid? Created_By { get; set; }
        public string? OwnerName { get; set; }
        public DateTime? Updated_On { get; set; }
        public Guid? Updated_By { get; set; }
        public Guid? Client_Id { get; set; }
        public string? Client_Name { get; set; }
        public DateTime? ClientValidFrom { get; set; }
        public string? Status { get; set; }

        // PROJECT JSON (returned from SQL)
        public string? ProjectsJson { get; set; }

        // Convert JSON → C# List<Project>
        
        public List<ProjectData> Projects =>
            string.IsNullOrEmpty(ProjectsJson)
                ? new List<ProjectData>()
                : JsonConvert.DeserializeObject<List<ProjectData>>(ProjectsJson);
    }
    public class ProjectData
    {
        public Guid? Id { get; set; }
        public string? ProjCode { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public Guid? Client_Id { get; set; }
        public string? Status { get; set; }
        public Guid? Repo_Id { get; set; }
    }



    //public class RepoData
    //{
    //    [Key]
    //    public Guid Repo_Id { get; set; }
    //    public string Repo_Code { get; set; }
    //    public string Title { get; set; }
    //    public string Description { get; set; }
    //    public DateTime Created_On { get; set; }
    //    public Guid Created_By { get; set; }
    //    public DateTime? Updated_On { get; set; }
    //    public Guid? Updated_By { get; set; }
    //    public Guid Client_Id { get; set; }
    //    public string? Status { get; set; }
    //}
}
