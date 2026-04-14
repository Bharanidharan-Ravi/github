// ─────────────────────────────────────────────────────────────────────────────
// Namespace : APIGateWay.BusinessLayer.Repository
// File      : LabelRepo.cs
//
// WHY NO DomainService.UpdateEntityWithAttachmentsAsync HERE:
//   That method uses FindAsync(Guid id) — LabelMaster PK is int, not Guid.
//   We use the DB context directly via a dedicated LabelService in domain layer.
//
// WHY NO SignalR broadcast:
//   Labels are master data — UI refreshes on next sync, no real-time push needed.
//   If you need it later, inject IRealtimeNotifier and follow ProjectRepo pattern.
//
// WHY NO sequence / no key generation:
//   PK is int IDENTITY — DB auto-assigns it on INSERT.
//
// AUDIT (Created_By, Updated_By):
//   These are varchar(100) string columns — NOT IAuditableUser interface.
//   We set them manually from ILoginContextService.userName.
// ─────────────────────────────────────────────────────────────────────────────

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
    public class LabelRepo : ILabelRepo
    {
        private readonly IDomainService _domainService;
        private readonly IMapper _mapper;
        private readonly ILoginContextService _loginContext;
        private readonly APIGatewayDBContext _db;
        private readonly IRequestStepContext _stepContext;            // ← ADDED

        public LabelRepo(
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

        // ─────────────────────────────────────────────────────────────────────
        // CREATE LABEL
        // Step log order:
        //   1. LabelMaster
        // ─────────────────────────────────────────────────────────────────────
        public async Task<GetLabel> CreateLabelAsync(CreateLabelDto dto)
        {
            var existingLabel = await _db.labelMaster
                .FirstOrDefaultAsync(label =>
                    label.Title == dto.Title || label.Color == dto.Color);

            if (existingLabel != null)
                throw new Exception("A label with the same title or color already exists");

            var created = await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var timer = _stepContext.StartStep();
                try
                {
                    var entity = new LabelMaster
                    {
                        Title = dto.Title,
                        Description = dto.Description,
                        Color = dto.Color,
                        Status = "Active",
                        Created_On = DateTime.Now,
                        Created_By = _loginContext.userName,
                        Updated_On = DateTime.Now,
                        Updated_By = _loginContext.userName
                    };

                    await _domainService.SaveEntityWithAttachmentsAsync(entity, null);

                    _stepContext.Success("LabelMaster", "INSERT", entity.Id.ToString(), timer);
                    return entity;
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("LabelMaster", "INSERT",
                        ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }
            });

            return _mapper.Map<GetLabel>(created);
        }

        // ─────────────────────────────────────────────────────────────────────
        // FULL UPDATE LABEL
        // Step log order:
        //   1. LabelMaster
        // ─────────────────────────────────────────────────────────────────────
        public async Task<GetLabel> UpdateLabelAsync(int id, UpdateLabelDto dto)
        {
            var updated = await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var timer = _stepContext.StartStep();
                try
                {
                    var result = await _domainService.UpdateEntityByIntIdAsync<LabelMaster>(id, entity =>
                    {
                        entity.Title = dto.Title;
                        entity.Description = dto.Description;
                        entity.Color = dto.Color;
                        entity.Updated_On = DateTime.Now;
                        entity.Updated_By = _loginContext.userName;

                        if (!string.IsNullOrWhiteSpace(dto.Status))
                            entity.Status = dto.Status;
                    });

                    _stepContext.Success("LabelMaster", "UPDATE", id.ToString(), timer);
                    return result;
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("LabelMaster", "UPDATE",
                        ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }
            });

            return _mapper.Map<GetLabel>(updated);
        }

        // ─────────────────────────────────────────────────────────────────────
        // STATUS UPDATE LABEL
        // Step log order:
        //   1. LabelMaster
        // ─────────────────────────────────────────────────────────────────────
        public async Task<GetLabel> UpdateLabelStatusAsync(int id, UpdateLabelStatusDto dto)
        {
            var updated = await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var timer = _stepContext.StartStep();
                try
                {
                    var result = await _domainService.UpdateEntityByIntIdAsync<LabelMaster>(id, entity =>
                    {
                        entity.Status = dto.Status;
                        entity.Updated_On = DateTime.Now;
                        entity.Updated_By = _loginContext.userName;
                    });

                    _stepContext.Success("LabelMaster", "UPDATE", id.ToString(), timer);
                    return result;
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("LabelMaster", "UPDATE",
                        ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }
            });

            return _mapper.Map<GetLabel>(updated);
        }
    }
}







#region Before Log 
//using APIGateWay.Business_Layer.Interface;
//using APIGateWay.DomainLayer.DBContext;
//using APIGateWay.DomainLayer.Interface;
//using APIGateWay.ModalLayer.GETData;
//using APIGateWay.ModalLayer.MasterData;
//using APIGateWay.ModalLayer.PostData;
//using AutoMapper;
//using Microsoft.EntityFrameworkCore;

//namespace APIGateWay.BusinessLayer.Repository
//{
//    public class LabelRepo : ILabelRepo
//    {
//        //private readonly ILabelService _labelService;
//        private readonly IDomainService _domainService;
//        private readonly IMapper _mapper;
//        private readonly ILoginContextService _loginContext;
//        private readonly APIGatewayDBContext _db;

//        public LabelRepo(
//            IDomainService domainService,
//            IMapper mapper,
//            ILoginContextService loginContext,
//            APIGatewayDBContext dBContext
//            )
//        {
//            _domainService = domainService;
//            _mapper = mapper;
//            _loginContext = loginContext;
//            _db = dBContext;
//        }

//        // ── CREATE ────────────────────────────────────────────────────────────
//        // POST /api/label
//        // Role 1 only — RouteAccessPolicy enforces this before reaching here
//        // LabelRepo.cs
//        public async Task<GetLabel> CreateLabelAsync(CreateLabelDto dto)
//        {
//            var existingLabel = await _db.labelMaster
//                .FirstOrDefaultAsync(label => label.Title == dto.Title || label.Color == dto.Color);

//            if (existingLabel != null)
//            {
//                throw new Exception("A label with the same title or color already exists");
//            }
//            var created = await _domainService.ExecuteInTransactionAsync(async () =>
//            {
//                var entity = new LabelMaster
//                {
//                    Title = dto.Title,
//                    Description = dto.Description,
//                    Color = dto.Color,
//                    Status = "Active",
//                    Created_On = DateTime.Now,
//                    Created_By = _loginContext.userName,
//                    Updated_On = DateTime.Now,
//                    Updated_By = _loginContext.userName
//                };

//                // SaveEntityWithAttachmentsAsync is generic — works for any entity
//                // LabelMaster has no attachments so pass null
//                await _domainService.SaveEntityWithAttachmentsAsync(entity, null);

//                // After SaveChangesAsync inside that method,
//                // EF populates entity.Id with the DB-assigned IDENTITY value
//                return entity;
//            });

//            return _mapper.Map<GetLabel>(created);
//        }
//        // ── FULL UPDATE ───────────────────────────────────────────────────────
//        // PUT /api/label/{id}
//        // Role 1 only — RouteAccessPolicy enforces this before reaching here
//        // What changes:    Title, Description, Color, Status (optional)
//        // What auto-sets:  Updated_On = now, Updated_By = current user
//        // What never changes: Id, Created_On, Created_By
//        public async Task<GetLabel> UpdateLabelAsync(int id, UpdateLabelDto dto)
//        {
//            var updated = await _domainService.ExecuteInTransactionAsync(async () =>
//            {
//                return await _domainService.UpdateEntityByIntIdAsync<LabelMaster>(id, entity =>
//                {
//                    entity.Title = dto.Title;
//                    entity.Description = dto.Description;
//                    entity.Color = dto.Color;
//                    entity.Updated_On = DateTime.Now;
//                    entity.Updated_By = _loginContext.userName;

//                    if (!string.IsNullOrWhiteSpace(dto.Status))
//                        entity.Status = dto.Status;
//                });
//            });

//            return _mapper.Map<GetLabel>(updated);
//        }

//        public async Task<GetLabel> UpdateLabelStatusAsync(int id, UpdateLabelStatusDto dto)
//        {
//            var updated = await _domainService.ExecuteInTransactionAsync(async () =>
//            {
//                return await _domainService.UpdateEntityByIntIdAsync<LabelMaster>(id, entity =>
//                {
//                    entity.Status = dto.Status;
//                    entity.Updated_On = DateTime.Now;
//                    entity.Updated_By = _loginContext.userName;
//                });
//            });

//            return _mapper.Map<GetLabel>(updated);
//        }

//        //public async Task DeleteLabelAsync(int id)
//        //{
//        //    await _domainService.ExecuteInTransactionAsync(async () =>
//        //    {
//        //        await _domainService.DeleteEntityByIntIdAsync<LabelMaster>(id);
//        //        return true;
//        //    });
//        //}
//    }
//}
#endregion