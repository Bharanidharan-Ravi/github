using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WGAPP.ModelLayer.GithubModal.MasterData
{
    public class LabelMaster
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Created_On { get; set; }
        public string Color { get; set; }
    }
}