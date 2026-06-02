using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.MasterData;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Session
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly IMapper _mapper;
        private readonly IDomainService _domainService;
        private readonly IRepoAccessService _repoAccessService;
        public NotificationRepository(IMapper mapper, IDomainService domainService, IRepoAccessService repoAccessService)
        {
            _mapper = mapper;
            _domainService = domainService;
            _repoAccessService = repoAccessService;
        }

        public async Task<Guid> CreateAsync(CreateNotificationRequest request)
        {
            var notification =
                _mapper.Map<NotificationMaster>(request);

            notification.NotificationId = Guid.NewGuid();

            await _domainService
                .SaveEntityAsync(notification);

            var audiences =
                request.Audiences
                    .Select(x =>
                    {
                        var audience =
                            _mapper.Map<NotificationAudience>(x);

                        audience.AudienceId = Guid.NewGuid();

                        audience.NotificationId =
                            notification.NotificationId;

                        return audience;
                    })
                    .ToList();

            await _domainService
                .SaveEntitiesAsync(audiences);

            return notification.NotificationId;
        }

        public async Task<int> GetUnreadCountAsync(
        Guid userId)
        {
            var userRepos =
                await _repoAccessService
                    .GetUserRepoGuidsAsync(userId);

            var repoIds =
                userRepos
                    .Select(x => x.RepoId.ToString())
                    .ToList();

            var lastSeen =
                await _domainService
                    .Query<NotificationUserState>()
                    .Where(x => x.UserId == userId)
                    .Select(x => (DateTime?)x.LastSeenAt)
                    .FirstOrDefaultAsync();

            var lastSeenDate =
                lastSeen ?? DateTime.MinValue;

            // Admin / No Repo Access Fallback
            if (!repoIds.Any())
            {
                return await (
                    from n in _domainService.Query<NotificationMaster>()
                    where n.CreatedAt > lastSeenDate
                    && n.ActorId != userId
                    select n.NotificationId
                )
                .Distinct()
                .CountAsync();
            }

            return await (
                from n in _domainService.Query<NotificationMaster>()

                join a in _domainService.Query<NotificationAudience>()
                    on n.NotificationId equals a.NotificationId

                where
                    a.AudienceType == "REPOSITORY"
                    &&
                    repoIds.Contains(a.AudienceValue)
                    &&
                    n.CreatedAt > lastSeenDate
                    && n.ActorId != userId

                select n.NotificationId
            )
            .Distinct()
            .CountAsync();
        }
        public async Task EnsureUserStateAsync(
        Guid userId)
        {
            var exists =
                await _domainService
                    .Query<NotificationUserState>()
                    .AnyAsync(x => x.UserId == userId);

            if (exists)
                return;

            await _domainService
                .SaveEntityAsync(
                    new NotificationUserState
                    {
                        UserId = userId,

                        LastSeenAt = DateTime.UtcNow,

                        UpdatedAt = DateTime.UtcNow
                    });
        }

        public async Task<List<NotificationListResponse>>
       GetNotificationsAsync(
           Guid userId)
        {
            var userRepos =
                await _repoAccessService
                    .GetUserRepoGuidsAsync(userId);

            var repoIds =
                userRepos
                    .Select(x => x.RepoId.ToString())
                    .ToList();

            IQueryable<NotificationMaster> query;

            if (!repoIds.Any())
            {
                query =
                    _domainService
                        .Query<NotificationMaster>()
                        .Where(x =>
                            x.ActorId != userId);
            }
            else
            {
                query =
                    from n in _domainService.Query<NotificationMaster>()

                    join a in _domainService.Query<NotificationAudience>()
                        on n.NotificationId equals a.NotificationId

                    where
                        repoIds.Contains(
                            a.AudienceValue)
                        &&
                        n.ActorId != userId

                    select n;
            }

            return await query
                .OrderByDescending(x => x.CreatedAt)
                .Take(50)
                .Select(n =>
                    new NotificationListResponse
                    {
                        NotificationId =
                            n.NotificationId,

                        Title =
                            n.Title,

                        Message =
                            n.Message,

                        EntityType =
                            n.EntityType,

                        EntityId =
                            n.EntityId.ToString(),

                        CreatedAt =
                            n.CreatedAt
                    })
                .ToListAsync();
        }
    }
}
