using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.PostHelper;

namespace APIGateWay.ModalLayer.PostData
{
    public class ProjectDto
    {
        public string Title { get; set; }
        public string Description { get; set; }
        //public string RepoKey { get; set; }
        public Guid? Responsible { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public Guid? Repo_Id { get; set; }
        public string? HtmlDesc { get; set; }
        public TempReturn? temp { get; set; }
    }
}
