//using APIGateWay.DomainLayer.Interface;
//using APIGateWay.ModalLayer.Hub;
//using APIGateWay.ModalLayer.PostData;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace APIGateWay.DomainLayer.Service
//{
//    public class UpdateEntity
//    {
//        public async Task<TResponse> UpdateEntityWithAttachmentsAsync<TEntity, TDto, TResponse>(
//    Guid id,
//    TDto dto,
//    Func<TEntity, string> htmlSelector,
//    Action<TEntity, string> htmlSetter,
//    string entityName)
//    where TEntity : BaseEntity
//        {
//            ProcessedAttachmentResult attachmentResult = null;
//            TResponse finalResult = default;

//            finalResult = await _domainService.ExecuteInTransactionAsync(async () =>
//            {
//                var entity = await _dbContext.Set<TEntity>()
//                    .Include(x => x.Attachments)
//                    .FirstOrDefaultAsync(x => x.Id == id);

//                if (entity == null)
//                    throw new Exception($"{entityName} not found");

//                // 1️⃣ Preserve immutable fields
//                var existingKey = entity.GetType().GetProperty("ProjectKey")?.GetValue(entity);
//                var existingSiNo = entity.GetType().GetProperty("SiNo")?.GetValue(entity);

//                // 2️⃣ Map allowed fields
//                _mapper.Map(dto, entity);

//                // 3️⃣ Restore immutable fields
//                if (existingKey != null)
//                    entity.GetType().GetProperty("ProjectKey")?.SetValue(entity, existingKey);

//                if (existingSiNo != null)
//                    entity.GetType().GetProperty("SiNo")?.SetValue(entity, existingSiNo);

//                // 4️⃣ Attachment Sync
//                var newHtml = htmlSelector(entity);
//                var oldAttachments = entity.Attachments.Where(a => a.Status == 1).ToList();

//                attachmentResult = await _attachmentService.ProcessAndSyncAttachmentsAsync(
//                    newHtml,
//                    oldAttachments,
//                    entity.Id.ToString(),
//                    entityName
//                );

//                htmlSetter(entity, attachmentResult.UpdatedHtml);

//                // 5️⃣ Mark removed attachments inactive
//                foreach (var removed in attachmentResult.RemovedAttachments)
//                {
//                    removed.Status = 2;
//                }

//                // 6️⃣ Add new attachments
//                if (attachmentResult.NewAttachments?.Any() == true)
//                {
//                    await _dbContext.AddRangeAsync(attachmentResult.NewAttachments);
//                }

//                // 7️⃣ Audit update
//                var audit = new StatusAudit
//                {
//                    Id = Guid.NewGuid(),
//                    EntityId = entity.Id,
//                    EntityName = entityName,
//                    OldStatus = entity.Status,
//                    NewStatus = entity.Status,
//                    ChangedBy = _loginContext.userId,
//                    ChangedAt = DateTime.UtcNow
//                };

//                _dbContext.Add(audit);

//                await _dbContext.SaveChangesAsync();

//                return _mapper.Map<TResponse>(entity);
//            });

//            // 🔥 Broadcast AFTER COMMIT
//            try
//            {
//                await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
//                {
//                    Entity = entityName,
//                    Action = "Update",
//                    Payload = finalResult,
//                    KeyField = "Id",
//                    RepoKey = (string)finalResult.GetType().GetProperty("RepoKey")?.GetValue(finalResult),
//                    Timestamp = DateTime.UtcNow
//                });
//            }
//            catch { }

//            return finalResult;
//        }
//    }
//}
