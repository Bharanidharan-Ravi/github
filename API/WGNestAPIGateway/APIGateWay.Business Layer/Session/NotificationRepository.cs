using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.Business_Layer.Interface; // Ensure this is the correct namespace for ILoginContextService
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace APIGateWay.Business_Layer.Session
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly IMapper _mapper;
        private readonly IDomainService _domainService;
        private readonly IRepoAccessService _repoAccessService;
        private readonly ILoginContextService _loginContext; // <-- NEW

        public NotificationRepository(IMapper mapper, IDomainService domainService, IRepoAccessService repoAccessService, ILoginContextService loginContext)
        {
            _mapper = mapper;
            _domainService = domainService;
            _repoAccessService = repoAccessService;
            _loginContext = loginContext;
        }

        public async Task<Guid> CreateAsync(CreateNotificationRequest request)
        {
            var notification = _mapper.Map<NotificationMaster>(request);
            notification.NotificationId = Guid.NewGuid();
            await _domainService.SaveEntityAsync(notification);

            var audiences = request.Audiences.Select(x => {
                var audience = _mapper.Map<NotificationAudience>(x);
                audience.AudienceId = Guid.NewGuid();
                audience.NotificationId = notification.NotificationId;
                return audience;
            }).ToList();

            await _domainService.SaveEntitiesAsync(audiences);
            return notification.NotificationId;
        }

        public async Task<int> GetUnreadCountAsync(Guid userId)
        {
            var userRepos = await _repoAccessService.GetUserRepoGuidsAsync(userId);
            var repoIds = userRepos.Select(x => x.RepoId.ToString()).ToList();
            var lastSeenDate = await _domainService.Query<NotificationUserState>()
                .Where(x => x.UserId == userId).Select(x => (DateTime?)x.LastSeenAt).FirstOrDefaultAsync() ?? DateTime.MinValue;

            var role = _loginContext.role;
            IQueryable<NotificationMaster> query;

            if (role == 3) // Client Logic
            {
                query = from n in _domainService.Query<NotificationMaster>()
                        join a in _domainService.Query<NotificationAudience>() on n.NotificationId equals a.NotificationId
                        where a.AudienceType == "REPOSITORY" && repoIds.Contains(a.AudienceValue)
                           && n.CreatedAt > lastSeenDate && n.ActorId != userId
                        select n;
            }
            else // Admin/Employee Logic (ONLY see if assigned)
            {
                query = from n in _domainService.Query<NotificationMaster>()
                        join a in _domainService.Query<NotificationAudience>() on n.NotificationId equals a.NotificationId
                        where a.AudienceType == "USER" && a.AudienceValue == userId.ToString()
                           && n.CreatedAt > lastSeenDate && n.ActorId != userId
                        select n;
            }

            return await query.Distinct().CountAsync();
        }

        public async Task<List<NotificationListResponse>> GetNotificationsAsync(Guid userId)
        {
            var userRepos = await _repoAccessService.GetUserRepoGuidsAsync(userId);
            var repoIds = userRepos.Select(x => x.RepoId.ToString()).ToList();
            var role = _loginContext.role;

            IQueryable<NotificationMaster> query;

            if (role == 3) // Client Logic
            {
                query = from n in _domainService.Query<NotificationMaster>()
                        join a in _domainService.Query<NotificationAudience>() on n.NotificationId equals a.NotificationId
                        where a.AudienceType == "REPOSITORY" && repoIds.Contains(a.AudienceValue) && n.ActorId != userId
                        select n;
            }
            else // Admin/Employee Logic (ONLY see if assigned)
            {
                query = from n in _domainService.Query<NotificationMaster>()
                        join a in _domainService.Query<NotificationAudience>() on n.NotificationId equals a.NotificationId
                        where a.AudienceType == "USER" && a.AudienceValue == userId.ToString() && n.ActorId != userId
                        select n;
            }

            return await query.Distinct().OrderByDescending(x => x.CreatedAt).Take(50)
                .Select(n => new NotificationListResponse
                {
                    NotificationId = n.NotificationId,
                    Title = n.Title,
                    Message = n.Message,
                    EntityType = n.EntityType,
                    EntityId = n.EntityId.ToString(),
                    CreatedAt = n.CreatedAt
                }).ToListAsync();
        }

        public async Task EnsureUserStateAsync(Guid userId) { /* Keep exact same code as before */ }
        public async Task MarkSeenAsync(Guid userId) { /* Keep exact same code as before */ }
    }
}