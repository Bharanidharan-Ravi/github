using APIGateWay.Business_Layer.Interface;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.MasterData;
using System.Text.Json;

namespace APIGateWay.Business_Layer.Repository
{
    public class TicketHistoryRepository : ITicketHistoryRepository
    {
        private readonly APIGatewayDBContext _db;

        public TicketHistoryRepository(APIGatewayDBContext db)
        {
            _db = db;
        }

        public async Task LogAsync(TicketHistoryEntry entry)
        {
            try
            {
                var row = BuildRow(entry);
                _db.TicketHistories.Add(row);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // History failure MUST NOT break the main operation
                // Log to console — replace with your logger if available
                Console.WriteLine(
                    $"[TicketHistoryService] Failed to log event '{entry.EventType}' " +
                    $"for ticket {entry.IssueId}: {ex.Message}");
            }
        }

        public async Task LogManyAsync(IEnumerable<TicketHistoryEntry> entries)
        {
            try
            {
                var rows = entries.Select(BuildRow).ToList();
                _db.TicketHistories.AddRange(rows);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[TicketHistoryService] Failed to log batch events: {ex.Message}");
            }
        }

        private static TicketHistory BuildRow(TicketHistoryEntry entry)
        {
            string? metaJson = null;
            if (entry.Meta != null)
            {
                try { metaJson = JsonSerializer.Serialize(entry.Meta); }
                catch { /* ignore serialization errors */ }
            }

            return new TicketHistory
            {
                IssueId = entry.IssueId,
                EventType = entry.EventType,
                Summary = entry.Summary,
                FieldName = entry.FieldName,
                OldValue = entry.OldValue,
                NewValue = entry.NewValue,
                ActorId = entry.ActorId,
                ActorName = entry.ActorName,
                WorkStreamId = entry.WorkStreamId,
                ThreadId = entry.ThreadId,
                TargetEntityId = entry.TargetEntityId,
                TargetEntityType = entry.TargetEntityType,
                MetaJson = metaJson,
                CreatedAt = DateTime.UtcNow,
            };
        }
    }
}
