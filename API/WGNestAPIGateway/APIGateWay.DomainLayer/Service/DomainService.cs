using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.PostData;
using APIGateWay.ModelLayer.ErrorException;
using System;

namespace APIGateWay.DomainLayer.Service
{
    public class DomainService : IDomainService
    {
        private readonly APIGatewayDBContext _dBContext;
        public DomainService(APIGatewayDBContext dBContext)
        {
            _dBContext = dBContext;
        }
        // 1. The Transaction Wrapper
        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> businessLogic)
        {
            using var transaction = await _dBContext.Database.BeginTransactionAsync();
            try
            {
                var result = await businessLogic(); // This runs your Business Layer code

                await transaction.CommitAsync();    // Commit if everything succeeds
                return result;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();  // Rollback SQL if anything fails
                throw;
            }
        }

        // 2. The ✨ GENERIC ✨ Save Method
        // This will accept ProjectMaster, RepositoryMaster, IssueMaster, etc.!
        public async Task SaveEntityWithAttachmentsAsync<TEntity>(TEntity entity, List<AttachmentMaster> attachments) where TEntity : class
        {
            _dBContext.Set<TEntity>().Add(entity);

            if (attachments != null && attachments.Any())
            {
                _dBContext.AttachmentMaster.AddRange(attachments);
            }

            await _dBContext.SaveChangesAsync();
        }

        // ── NEW: Generic Update + Attachments ─────────────────────────────────
        //
        // HOW IT WORKS:
        //   1. Finds the entity by its primary key (Guid id)
        //   2. Calls mutator(entity) — YOUR lambda changes only the fields you list
        //   3. EF change tracker sees exactly what changed
        //   4. SaveChangesAsync fires — DBContext audit intercepts it:
        //        UpdatedAt = India time NOW       (auto)
        //        UpdatedBy = current userId       (auto)
        //        CreatedAt / CreatedBy protected  (IsModified = false, auto)
        //   5. New attachments added if supplied — old ones stay untouched
        //
        // NO sequence call — keys/numbers never change on update.
        //
        // USED BY:
        //   Full update   → mutator sets Title, HtmlDesc, Description, DueDate, etc.
        //   Status update → mutator sets ONLY Status, nothing else
        //
        public async Task<TEntity> UpdateEntityWithAttachmentsAsync<TEntity>(
            Guid id,
            Action<TEntity> mutator,
            List<AttachmentMaster>? newAttachments = null)
            where TEntity : class
        {
            // Find tracked entity — EF will detect changes on it
            var entity = await _dBContext.Set<TEntity>().FindAsync(id);

            if (entity == null)
                throw new Exceptionlist.DataNotFoundException(
                    $"{typeof(TEntity).Name} with Id '{id}' not found.");

            // Apply ONLY the fields the caller listed in the lambda
            // Fields not mentioned here → EF never marks them Modified → DB ignores them
            mutator(entity);

            // Add new attachments if any (old ones are never deleted here)
            if (newAttachments != null && newAttachments.Any())
                _dBContext.AttachmentMaster.AddRange(newAttachments);

            // DBContext.SaveChangesAsync audit (your existing override):
            //   → sets UpdatedAt, UpdatedBy automatically for Modified entities
            //   → CreatedAt, CreatedBy IsModified = false, never touched
            await _dBContext.SaveChangesAsync();

            return entity;
        }
    }
}
