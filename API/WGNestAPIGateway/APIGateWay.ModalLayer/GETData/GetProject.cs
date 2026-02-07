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
        public Guid? Client_Id { get; set; }
        public string? Client_Name { get; set; }
        public Guid? Repo_Id { get; set; }
        public string Repo_Name { get; set; }
        public string Status { get; set; }
        public string EmployeeName { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime Created_On { get; set; }
    }
}
