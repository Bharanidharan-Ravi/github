using APIGateWay.Business_Layer.Helper.Events.Eventhelper;
using APIGateWay.Business_Layer.Helper.Events.Interface;
using APIGateWay.Business_Layer.Session;
using APIGateWay.BusinessLayer.Helper;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.Helper;
using APIGateWay.ModalLayer.Hub;
using APIGateWay.ModalLayer.MasterData;

namespace APIGateWay.Business_Layer.Helper.Events.Services
{
    public class EventCenter : IEventCenter
    {
        private readonly IEventContextProvider _contextProvider;
        private readonly ISyncExecutionService _syncExecutionService;
        private readonly INotificationRepository _notificationRepository;
        private readonly IRealtimeNotifier _realtimeNotifier;

        public EventCenter (IEventContextProvider contextProvider, ISyncExecutionService syncExecutionService, INotificationRepository notification ,IRealtimeNotifier realtimeNotifier)
        {
            _contextProvider = contextProvider;
            _syncExecutionService = syncExecutionService;
            _notificationRepository = notification;
            _realtimeNotifier = realtimeNotifier;
        }
        private Dictionary<string, string> GetContextValues()
        {
            return _contextProvider.GetContextValues();
        }
        private Dictionary<string, string> BuildSyncParams(
        EventRequest request)
        {
            var syncParams =
                new Dictionary<string, string>();

            syncParams[request.KeyField] =
                request.EntityId.ToString();

            var contextValues = GetContextValues();

            foreach (var mapping in request.ContextMappings)
            {
                if (contextValues.TryGetValue(
                        mapping.Value,
                        out var value))
                {
                    syncParams[mapping.Key] = value;
                }
            }

            return syncParams;
        }
        public async Task PublishAsync(
         EventRequest request,
         bool notify = true,
         bool signalR = true)
        {
            try
            {
                Console.WriteLine(
                    $"Event Received : {request.EventType}");

                if (!SyncRepositoryConfigStore.Configs.TryGetValue(
                        request.ConfigKey,
                        out var cfg))
                {
                    Console.WriteLine(
                        $"Config not found : {request.ConfigKey}");

                    return;
                }

                var richData =
                 await FetchRichDataAsync(
                     request);


                if (richData == null)
                    return;

                var contextValues = GetContextValues();

                var actorId =
                    Guid.Parse(
                        contextValues["UserId"]);

                var actorName =
                    contextValues["UserName"];

                var title =
                ReflectionHelper.GetPropertyValue<string>(
                    richData,
                    request.TitleField);

                var code =
                    ReflectionHelper.GetPropertyValue<string>(
                        richData,
                        request.CodeField);

                var audienceId =
                    ReflectionHelper.GetPropertyValue<Guid?>(
                        richData,
                        request.AudienceField);

                if (signalR)
                {
                    await _realtimeNotifier.BroadcastAsync(
                        new RealtimeMessage
                        {
                            Entity = cfg.SignalREntity,

                            Action = cfg.SignalRAction,

                            Payload = richData,

                            KeyField = request.KeyField,

                            RepoKey =
                                audienceId.HasValue
                                    ? $"repo-{audienceId}"
                                    : "global-admin",

                            Timestamp = DateTime.UtcNow
                        });
                }

                if (notify)
                {
                    var notificationId = await _notificationRepository
                    .CreateAsync(
                        new CreateNotificationRequest
                        {
                            EventType = request.EventType,

                            EntityType = request.EntityType,

                            EntityId = request.EntityId,

                            RepositoryId = audienceId,

                            Title = title,

                            Message = request.MessageTemplate
                                .Replace("{Code}", code ?? string.Empty),

                            ActorId = actorId,

                            ActorName = actorName,

                            Audiences =
                            [
                                new()
                                {
                                    AudienceType = "REPOSITORY",

                                    AudienceValue =
                                        audienceId?.ToString()
                                }
                            ]
                        });
                    await _realtimeNotifier.BroadcastAsync(
                       new RealtimeMessage
                       {
                           Entity = "Notification",

                           Action = "Created",

                           Payload = new
                           {
                               NotificationId = notificationId,
                               CreatedByUserId = actorId
                           },

                           RepoKey =
                               audienceId.HasValue
                                   ? $"repo-{audienceId}"
                                   : "repo-global-admin",

                           Timestamp = DateTime.UtcNow
                       });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"EventCenter Error : {ex.Message}");
            }
        }

        private async Task<object?> FetchRichDataAsync(
        EventRequest request)
        {
            var syncParams =
            BuildSyncParams(request);

            var method =
                typeof(ServiceHelper)
                    .GetMethod(nameof(ServiceHelper.FetchRichDataAsync));

            var genericMethod =
                method!.MakeGenericMethod(
                    request.ResponseType);

            var fallback =
                Activator.CreateInstance(
                    request.ResponseType);

            var predicate =
                EventExpressionHelper.CreatePredicate(
                    request.ResponseType,
                    request.MatchField,
                    request.EntityId);

            var task =
                (Task)genericMethod.Invoke(
                    null,
                    new object[]
                    {
                _syncExecutionService,
                request.ConfigKey,
                syncParams,
                predicate,
                fallback,
                null
                    })!;

            await task;

            var resultProperty =
                task.GetType()
                    .GetProperty("Result");

            return resultProperty?.GetValue(task);
        }
    }
}
