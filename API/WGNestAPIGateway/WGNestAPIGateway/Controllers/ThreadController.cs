using APIGateWay.BusinessLayer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ThreadController : ControllerBase
    {
        private readonly IThreadsRepository _threadsRepository;

        public ThreadController(IThreadsRepository threadsRepository)
        {
            _threadsRepository = threadsRepository;
        }
        [HttpPost("CreateThread")]
        public async Task<IActionResult> PostThread([FromBody] PostThreadsDto threadDto)
        {
            var response = await _threadsRepository.CreateThreadAsync(threadDto);
            return Ok(ApiResponseHelper.Success(response, "thread create successfully."));
        }
    }
}


