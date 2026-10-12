using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface IBannermessageRepo
    {
        Task<GetBannerMessageSP> GetBannerMessageAsync(PostBannerMessageDto dto);
        Task<GetBannerMessageSP> UpdateBannerMessageAsync(Guid BannerMessageId, PutBannerMessageDto dto);

        Task<List<GetBannerMessageSP>> GetBannerMessagesAsync();
    }
}
