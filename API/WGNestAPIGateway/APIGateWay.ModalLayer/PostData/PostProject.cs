using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    public class ProjectPostDto
    {
        public int SiNo { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string HtmlDesc { get; set; }
        public string ProjectKey { get; set; }
        public string RepoKey { get; set; }
        public string Responsible { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime DueDate { get; set; }
        public string? Hours { get; set; }
        public  TempReturn tempReturn { get; set; }
    }
}
