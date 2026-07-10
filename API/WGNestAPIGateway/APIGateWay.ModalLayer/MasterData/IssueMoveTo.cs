using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    public class IssueMoveTo
    {
        [Key]
        public Guid Id { get; set; }

        public Guid Issue_Id { get; set; }

        public Guid Move_to { get; set; }

        public Guid CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
