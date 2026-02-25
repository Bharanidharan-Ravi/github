using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.PostData;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
