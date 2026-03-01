using APIGateWay.ModalLayer.PostData;
using System;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IDomainService
    {
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> businessLogic);
        Task SaveEntityWithAttachmentsAsync<TEntity>(TEntity entity, List<AttachmentMaster> attachments) where TEntity : class;

        // ── NEW ───────────────────────────────────────────────────────────────
        // Used for all update operations — full update and status-only update.
        // Finds entity by id, calls mutator to apply your changes, saves.
        // DBContext audit sets UpdatedAt + UpdatedBy automatically.
        // No sequence call — keys never change on update.
        Task<TEntity> UpdateEntityWithAttachmentsAsync<TEntity>(
            Guid id,
            Action<TEntity> mutator,
            List<AttachmentMaster>? newAttachments = null)
            where TEntity : class;
    }
}
