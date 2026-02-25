using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IDomainService
    {
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> businessLogic);
        Task SaveEntityWithAttachmentsAsync<TEntity>(TEntity entity, List<AttachmentMaster> attachments) where TEntity : class;
    }
}
