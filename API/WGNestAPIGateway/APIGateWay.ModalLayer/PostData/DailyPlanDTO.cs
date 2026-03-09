using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    public class CreateDailyPlanDto
    {
        public Guid TicketId { get; set; }
        public string? ProjKey { get; set; }
    }

    // PATCH /api/dailyplan/{id}/uncheck — comment is mandatory
    public class UncheckPlanDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MaxLength(500)]
        public string UncheckComment { get; set; } = string.Empty;
    }
}
