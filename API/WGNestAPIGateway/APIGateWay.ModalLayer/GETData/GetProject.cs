using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetProject
    {
        public Guid Id { get; set; }
        public string Project_Name { get; set; }
        public string Description { get; set; }
        public string? HtmlDesc { get; set; }
        public string? ProjectKey { get; set; }
        public Guid? Repo_Id { get; set; }
        public string? RepoKey { get; set; }
        public string? Repo_Name { get; set; }
        public string Status { get; set; }
        public Guid? Responsible { get; set; }
        public string? EmployeeName { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

    }
}