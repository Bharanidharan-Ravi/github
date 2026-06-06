using APIGateWay.Business_Layer.Helper.Events.Eventhelper;
using APIGateWay.Business_Layer.Helper.Events.Interface;
using APIGateWay.Business_Layer.Session;
using APIGateWay.BusinessLayer.Helper;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.Helper;
using APIGateWay.ModalLayer.Hub;
using APIGateWay.ModalLayer.MasterData;
// Ensure you have the using statement for wherever CreateNotificationRequest is defined

namespace APIGateWay.Business_Layer.Helper.Events.Services
{
    public class EventCenter : IEventCenter
    {
        private readonly IEventContextProvider _contextProvider;
        private readonly ISyncExecutionService _syncExecutionService;
        private readonly INotificationRepository _notificationRepository;
        private readonly IRealtimeNotifier _realtimeNotifier;

        public EventCenter(IEventContextProvider contextProvider, ISyncExecutionService syncExecutionService, INotificationRepository notification, IRealtimeNotifier realtimeNotifier)
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

        private Dictionary<string, string> BuildSyncParams(EventRequest request)
        {
            var syncParams = new Dictionary<string, string>();
            syncParams[request.KeyField] = request.EntityId.ToString();
            var contextValues = GetContextValues();

            foreach (var mapping in request.ContextMappings)
            {
                if (contextValues.TryGetValue(mapping.Value, out var value))
                {
                    syncParams[mapping.Key] = value;
                }
            }
            return syncParams;
        }

        public async Task<T?> PublishAsync<T>(EventRequest request, bool notify = true, bool signalR = true)
        {
            try
            {
                Console.WriteLine($"Event Received : {request.EventType}");

                if (!SyncRepositoryConfigStore.Configs.TryGetValue(request.ConfigKey, out var cfg))
                {
                    Console.WriteLine($"Config not found : {request.ConfigKey}");
                    return default;
                }

                var richData = await FetchRichDataAsync(request);

                if (richData == null)
                    return default;

                var audienceId = ReflectionHelper.GetPropertyValue<Guid?>(richData, request.AudienceField);
                var assigneeId = ReflectionHelper.GetPropertyValue<Guid?>(richData, request.AssigneeField);
                var resourceIdsObj = ReflectionHelper.GetPropertyValue<object>(richData, request.ResourceIdsField);


                var contextValues = GetContextValues();
                var actorId = Guid.Parse(contextValues["UserId"]);
                var actorName = contextValues["UserName"];

                var title = ReflectionHelper.GetPropertyValue<string>(richData, request.TitleField);
                var code = ReflectionHelper.GetPropertyValue<string>(richData, request.CodeField);
                

                // 2. BROADCAST LIVE TICKET UPDATE TO EVERYONE IN REPO (Keeps UI fast/sync'd for everyone)
                if (signalR)
                {
                    await _realtimeNotifier.BroadcastAsync(
                        new RealtimeMessage
                        {
                            Entity = cfg.SignalREntity,
                            Action = cfg.SignalRAction,
                            Payload = richData,
                            KeyField = request.KeyField,
                            RepoKey = audienceId.HasValue ? $"repo-{audienceId}" : "global-admin",
                            Timestamp = DateTime.UtcNow
                        });
                }

                // 3. PERSIST DB NOTIFICATION AND PING BELL ICONS
                if (notify)
                {
                    // 1. DECLARE IT HERE: Build a unique list of all targeted users
                    var targetUsers = new HashSet<Guid>();

                    // 2. Add the primary Assignee to the list (if one exists)
                    if (assigneeId.HasValue)
                    {
                        targetUsers.Add(assigneeId.Value);
                    }

                    // 3. YOUR LOOP: Safely extract Guids from the All_Assignees list
                    if (resourceIdsObj is string jsonString && !string.IsNullOrWhiteSpace(jsonString))
                    {
                        try
                        {
                            // Deserialize the JSON string into a list of generic objects (dictionaries)
                            var resList = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonString);

                            if (resList != null)
                            {
                                foreach (var item in resList)
                                {
                                    // Check if the dictionary contains our key
                                    if (item.TryGetValue("Assignee_Id", out var valObj))
                                    {
                                        // JSON deserializer often reads Guids as strings, so we parse it
                                        if (valObj != null)
                                        {
                                            var strVal = valObj.ToString();
                                            if (Guid.TryParse(strVal, out var parsedGuid))
                                            {
                                                targetUsers.Add(parsedGuid);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            Console.WriteLine($"Failed to deserialize All_Assignees JSON: {ex.Message}");
                        }
                    }

                    // 4. Build Audiences for the Database using our targetUsers list
                    var audiences = new List<NotificationAudience>();

                    // 🔥 ONLY add Client if the flag is true
                    if (audienceId.HasValue && request.NotifyRepo)
                    {
                        audiences.Add(new() { AudienceType = "REPOSITORY", AudienceValue = audienceId.Value.ToString() });
                    }

                    // 🔥 ONLY add Employees if the flag is true
                    if (request.NotifyUsers)
                    {
                        foreach (var userId in targetUsers)
                        {
                            audiences.Add(new() { AudienceType = "USER", AudienceValue = userId.ToString() });
                        }
                    }

                    // 5. Save the single notification with multiple audiences to the DB
                    if (audiences.Any())
                    {
                        var notificationId = await _notificationRepository.CreateAsync(new CreateNotificationRequest
                        {
                            EventType = request.EventType,
                            EntityType = request.EntityType,
                            EntityId = request.EntityId,
                            RepositoryId = audienceId,
                            Title = title,
                            Message = request.MessageTemplate.Replace("{Code}", code ?? string.Empty),
                            ActorId = actorId,
                            ActorName = actorName,
                            Audiences = audiences
                        });

                        // 5. SignalR Ping for Client's Bell Icon
                        if (audienceId.HasValue && request.NotifyRepo)
                        {
                            await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                            {
                                Entity = "Notification",
                                Action = "Created",
                                Payload = new { NotificationId = notificationId, CreatedByUserId = actorId },
                                KeyField = "NotificationId",
                                RepoKey = audienceId.Value.ToString(),
                                Timestamp = DateTime.UtcNow
                            });
                        }

                        // 6. SignalR Ping for EVERY assigned User's Bell Icon
                        if (request.NotifyUsers)
                        {
                            foreach (var userId in targetUsers)
                            {
                                await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                                {
                                    Entity = "Notification",
                                    Action = "Created",
                                    Payload = new { NotificationId = notificationId, CreatedByUserId = actorId },
                                    KeyField = "NotificationId",
                                    TargetUserId = userId,
                                    Timestamp = DateTime.UtcNow
                                });
                            }
                        }
                    }
                }
                if (richData is not T typedData)
                {
                    throw new InvalidOperationException($"Expected {typeof(T).Name}, got {richData?.GetType().Name}");
                }

                return typedData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EventCenter Error : {ex.Message}");
                return default;
            }
        }

        private async Task<object?> FetchRichDataAsync(EventRequest request)
        {
            var syncParams = BuildSyncParams(request);
            var method = typeof(ServiceHelper).GetMethod(nameof(ServiceHelper.FetchRichDataAsync));
            var genericMethod = method!.MakeGenericMethod(request.ResponseType);
            var fallback = Activator.CreateInstance(request.ResponseType);
            var predicate = EventExpressionHelper.CreatePredicate(request.ResponseType, request.MatchField, request.EntityId);

            var task = (Task)genericMethod.Invoke(null, new object[]
            {
                _syncExecutionService,
                request.ConfigKey,
                syncParams,
                predicate,
                fallback,
                null
            })!;

            await task;
            var resultProperty = task.GetType().GetProperty("Result");
            return resultProperty?.GetValue(task);
        }
    }
}