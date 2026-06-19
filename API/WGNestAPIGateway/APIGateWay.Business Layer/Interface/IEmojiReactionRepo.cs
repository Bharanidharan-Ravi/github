using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Interface
{
    public interface IEmojiReactionRepo
    {
        Task<Emoji_Reactions> CreateAsync(PostEmoji dto);
        Task DeleteAsync(int id);
    }
}
