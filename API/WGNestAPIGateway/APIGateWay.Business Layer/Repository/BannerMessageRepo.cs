using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Repository
{
    public class BannerMessageRepo:IBannermessageRepo
      
    {
        private readonly IDomainService _domainService;
        private readonly APIGateWayCommonService _commonService;
        private readonly IMapper _mapper;
        private readonly ILoginContextService _loginContext;
        private readonly IAttachmentService _attachmentService;
        private readonly IHelperGetData _helperGet;
        private readonly IRealtimeNotifier _realtimeNotifier;
        private readonly ISyncExecutionService _syncExecutionService;
        private readonly APIGatewayDBContext _dBContext;
        private readonly IWorkStreamService _workStreamService;
        private readonly IRequestStepContext _stepContext;            // ← ADDED

        public BannerMessageRepo(
            IDomainService domainService,
            APIGateWayCommonService service,
            APIGatewayDBContext dbContext,
            IMapper mapper,
            ILoginContextService loginContext,
            IAttachmentService attachmentService,
            IHelperGetData helperGet,
            IRealtimeNotifier realtimeNotifier,
            ISyncExecutionService syncExecutionService,
            IWorkStreamService workStreamService,
            IRequestStepContext stepContext)                          // ← ADDED
        {
            _domainService = domainService;
            _commonService = service;
            _mapper = mapper;
            _loginContext = loginContext;
            _attachmentService = attachmentService;
            _helperGet = helperGet;
            _realtimeNotifier = realtimeNotifier;
            _syncExecutionService = syncExecutionService;
            _dBContext = dbContext;
            _workStreamService = workStreamService;
            _stepContext = stepContext;                      // ← ADDED
        }
        public async Task<GetBannerMessageSP> GetBannerMessageAsync(PostBannerMessageDto dto)
        {
            GetBannerMessageSP finalData = null;
            try
            {
                finalData = await _domainService.ExecuteInTransactionAsync<GetBannerMessageSP>(async () =>
                {
                    var entity = _mapper.Map<BannerMessageMaster>(dto);
                    entity.BannerMessageId = Guid.NewGuid();
                    entity.Status = "Active";
                    entity.CreatedBy = _loginContext.userId;
                    await _dBContext.Set<BannerMessageMaster>().AddAsync(entity);
                    entity.CreatedAt = DateTime.UtcNow;
                    entity.UpdatedAt = DateTime.UtcNow;
                    entity.UpdatedBy = _loginContext.userId;

                    var timer = _stepContext.StartStep();
                    try
                    {
                        await _dBContext.SaveChangesAsync();
                        _stepContext.Success("BannerMessage ", "INSERT", entity.BannerMessageId.ToString(), timer);
                    }
                    catch (Exception ex)
                    {
                        _stepContext.Failure("BannerMessage ", "INSERT",
                            ex.Message, ex.InnerException?.Message, timer);
                        throw;
                    }
                    return _mapper.Map<GetBannerMessageSP>(entity);

                });
            }
            catch (Exception ex)
            {
                throw new Exception($"BannerMessage creation failed.Everything was rolled back safely.{ex}", ex);
            }

            return finalData;
        }

        public async Task<GetBannerMessageSP> UpdateBannerMessageAsync(Guid BannerMessageId, PutBannerMessageDto dto)
        {
            GetBannerMessageSP finalData = null;
            try
            {
                finalData = await _domainService.ExecuteInTransactionAsync<GetBannerMessageSP>(async () =>
                {
                    var entity = await _dBContext.BannerMessageMaster.FirstOrDefaultAsync
                    ( x => x.BannerMessageId == BannerMessageId);
                    if (entity == null)
                        throw new Exception("Banner message not found.");
                    entity.Status = dto.Status;
                    entity.MessageText = dto.MessageText;
                    entity.MessageTypeId=dto.MessageTypeId;
                    entity.StartDate = dto.StartDate;
                    entity.EndDate = dto.EndDate;
                    entity.UpdatedAt = DateTime.UtcNow;
                    entity.UpdatedBy = _loginContext.userId;

                    var timer = _stepContext.StartStep();
                    try
                    {
                        await _dBContext.SaveChangesAsync();
                        _stepContext.Success("BannerMessage ", "UPDATE", entity.BannerMessageId.ToString(), timer);
                    }
                    catch (Exception ex)
                    {
                        _stepContext.Failure("BannerMessage ", "UPDATE",
                            ex.Message, ex.InnerException?.Message, timer);
                        throw;
                    }
                    return _mapper.Map<GetBannerMessageSP>(entity);

                });
            }
            catch (Exception ex)
            {
                throw new Exception($"BannerMessage updated failed.Everything was rolled back safely.{ex}", ex);
            }

            return finalData;
        }

        public async Task<List<GetBannerMessageSP>> GetBannerMessagesAsync()
        {
            var data=await _dBContext.BannerMessageMaster
                .OrderByDescending(x=>x.CreatedAt) .ToListAsync();
            return _mapper.Map<List<GetBannerMessageSP>>(data);
        }

    }
}
