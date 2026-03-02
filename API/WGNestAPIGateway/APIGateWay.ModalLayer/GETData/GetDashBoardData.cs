using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData


{

    public class DashBoard
    {

        public List<GetDashBoardData> DashBoardData { get; set; }
        public List<Count> TicketCount { get; set; }
       
    }
    public class GetDashBoardData
    {

        [Key]

        public Guid Issue_Id { get; set; }

        public string? Title { get; set; }

        public Guid CreatedBy { get; set; }

        public string? Assinged_By { get; set; }

        public DateTime CreatedAt { get; set; }

        public Guid Project_Id { get; set; }

        public string? Project_Name { get; set; }

        public Guid Assignee_Id { get; set; }


        ////public DateTime Due_Date { get; set; }
        [JsonIgnore] // Ignore original Due_Date from serialization
        public DateTime Due_Date { get; set; }

        public string DueDate => Due_Date.ToString("yyyy-MM-dd");

        public string? Status { get; set; }

        public string? Issue_Code { get; set; }

        public DateTime UpdatedAt { get; set; }
        [JsonIgnore]
        public string Updated_At => UpdatedAt.ToString("yyyy-MM-dd");

        public Guid UpdatedBy { get; set; }

        public TimeOnly? From_Time { get; set; }

        public TimeOnly? To_Time { get; set; }

        public string? Hours { get; set; }

        public string? RepoKey { get; set; }

        public string? Repo_Name { get; set; }
        public Guid RepoId { get; set; }

       

    }


    public class Count
    {
        [Key]
        public int Total_Count { get; set; }
    }

    public class ThreadTimeSheetData
    {
        [Key]
        public int ThreadId { get; set; }

        public string? EmployeeName { get; set; }

        public string? Repository_Name { get; set; }

        public string? Project_Name { get; set; }

        public string? TicketNo { get; set; }

        public string? TicketName { get; set; }

        public string? EstimatedHours { get; set; }

        public string? ConsumeTime { get; set; }

        public string? Comment { get; set; }


        public string? StartTime { get; set; }

        public string? EndTime { get; set; }


        public string? total { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}

