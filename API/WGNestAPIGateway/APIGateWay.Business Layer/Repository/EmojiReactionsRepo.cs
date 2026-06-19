using APIGateWay.Business_Layer.Interface;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace APIGateWay.BusinessLayer.Repository
{
    public class EmojiReactionRepo : IEmojiReactionRepo
    {
        private readonly IDomainService _domainService;
        private readonly IMapper _mapper;
        private readonly ILoginContextService _loginContext;
        private readonly APIGatewayDBContext _db;
        private readonly IRequestStepContext _stepContext;            // ← ADDED

        public EmojiReactionRepo(
            IDomainService domainService,
            IMapper mapper,
            ILoginContextService loginContext,
            APIGatewayDBContext dBContext,
            IRequestStepContext stepContext)                          // ← ADDED
        {
            _domainService = domainService;
            _mapper = mapper;
            _loginContext = loginContext;
            _db = dBContext;
            _stepContext = stepContext;                             // ← ADDED
        }
        public async Task<Emoji_Reactions> CreateAsync(PostEmoji dto)
        {
            var created=await _domainService.ExecuteInTransactionAsync(async()=>
            {
                var timer = _stepContext.StartStep();
                try
                {
                    var entity = new Emoji_Reactions
                    {
                        ThreadId = dto.ThreadId,
                        Emoji = dto.Emoji,
                        CreatedAt = DateTime.Now,
                        CreatedBy =_loginContext.userId,
                    };

                    await _domainService.SaveEntityWithAttachmentsAsync(entity, null);

                    _stepContext.Success("EmojiReaction", "INSERT", entity.Id.ToString(), timer);
                    return entity;
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("EmojiReaction", "INSERT",
                        ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }
            });

            return _mapper.Map<Emoji_Reactions>(created);
        }
        public async Task DeleteAsync(int id)
        {
            await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var timer = _stepContext.StartStep();
                try
                {
                    var entity = await _domainService.Query<Emoji_Reactions>()
                    .FirstOrDefaultAsync(x => x.Id == id);
                    if (entity == null)
                        throw new Exception($"Emoji reaction {id} not found");
                    await _domainService.DeleteEntityAsync(entity);
                    _stepContext.Success("EmojiReaction", "DELETE", id.ToString(), timer);
                    return entity;
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("EmojiReaction", "DELETE", ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }
            });
        }

    }
}
