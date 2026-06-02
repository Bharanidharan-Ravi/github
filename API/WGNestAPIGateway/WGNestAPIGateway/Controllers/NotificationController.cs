using APIGateWay.Business_Layer.Session;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController :ControllerBase
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly ILoginContextService _loginContext;
        public NotificationController(INotificationRepository notificationRepository, ILoginContextService loginContext)
        {
            _notificationRepository = notificationRepository;
            _loginContext = loginContext;
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var count =
                await _notificationRepository
                    .GetUnreadCountAsync(
                        _loginContext.userId
                        );
/*
            return Ok(
                new NotificationCountResponse
                {
                    UnreadCount = count
                });*/
           return Ok( count );
        }
        
        [HttpGet("list")]
        public async Task<IActionResult> GetNotificationsAsync()
        {
            var count =
                await _notificationRepository.GetNotificationsAsync(_loginContext.userId);
           return Ok( count );
        }
    }
}
