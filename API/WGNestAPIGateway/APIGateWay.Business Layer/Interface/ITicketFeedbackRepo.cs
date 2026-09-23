using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Interface
{
    public interface ITicketFeedbackRepo
    {
        Task<bool> SubmitFeedbackAsync(PostTicketFeedbackDto dto);
        Task<List<GetTicketFeedbackDto>> GetFeedbacksByTicketAsync(Guid ticketId);
    }
}
