using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Interface
{
    public interface ITicketHistoryRepository
    {
        // Write a single history event — called from repos and services
        // Always fire-and-forget pattern — history failure NEVER breaks the main action
        Task LogAsync(TicketHistoryEntry entry);

        // Batch write — for events that produce multiple rows (e.g. labels replaced)
        Task LogManyAsync(IEnumerable<TicketHistoryEntry> entries);
    }
}