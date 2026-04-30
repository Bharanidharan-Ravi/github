using APIGateWay.Business_Layer.Helper;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.Hub;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.SignalRHub.Middleware
{
    public class RealtimeBroadcaster : IRealtimeBroadcaster
    {
        private readonly IRealtimeNotifier _notifier;
        private readonly ISyncExecutionService _syncService;
        private readonly ILogger<RealtimeBroadcaster> _logger;

        public RealtimeBroadcaster(
            IRealtimeNotifier notifier,
            ISyncExecutionService syncService,
            ILogger<RealtimeBroadcaster> logger)
        {
            _notifier = notifier;
            _syncService = syncService;
            _logger = logger;
        }

        public async Task NotifyAsync<TDto>(
            BroadcastEntityConfig<TDto> config,
            string action,
            TDto baseDto) where TDto : class
        {
            try
            {
                // ── 1. Fetch the full rich DTO ────────────────────────────────
                var richDto = await _syncService.FetchRichDataAsync<TDto>(
                    configKey: config.SyncConfigKey,
                    syncParams: config.BuildSyncParams(baseDto),
                    matchPredicate: p => config.MatchPredicate(p, baseDto),
                    fallbackData: baseDto,
                    lastSync: null);

                if (richDto is null)
                {
                    _logger.LogWarning(
                        "[Broadcaster] FetchRichData returned null — " +
                        "falling back to base DTO for {Entity} {Action}.",
                        config.Entity, action);

                    // Fall back to the base DTO rather than silently skipping
                    richDto = baseDto;
                }

                // ── 2. Build the message ──────────────────────────────────────
                var message = new RealtimeMessage
                {
                    Entity = config.Entity,
                    Action = action,
                    Payload = richDto,
                    KeyField = config.KeyField,
                    RepoKey = config.GetRepoKey(richDto),
                    IssueId = config.GetIssueId?.Invoke(richDto),
                    Timestamp = DateTime.UtcNow,
                };

                // ── 3. Broadcast ──────────────────────────────────────────────
                await _notifier.BroadcastAsync(message);

                _logger.LogInformation(
                    "[Broadcaster] {Entity} {Action} broadcast OK → repo:{RepoKey}",
                    config.Entity, action, message.RepoKey ?? "none");
            }
            catch (Exception ex)
            {
                // Broadcast failure must NEVER surface to the API caller
                _logger.LogError(ex,
                    "[Broadcaster] Failed to broadcast {Entity} {Action}. " +
                    "API response is unaffected.",
                    config.Entity, action);
            }
        }
    }
}
