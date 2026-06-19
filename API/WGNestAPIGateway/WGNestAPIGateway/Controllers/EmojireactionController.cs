using APIGateWay.Business_Layer.Interface;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/[Controller]")]
    public class EmojiReactionController : ControllerBase
    {
        private readonly IEmojiReactionRepo _reactionRepo;

        public EmojiReactionController(IEmojiReactionRepo emojiReactionRepo)
        {
            _reactionRepo = emojiReactionRepo;
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/label
        // Role 1 only. Creates a new label.
        // Body: { "Title": "Bug", "Description": "...", "Color": "#FF5733" }
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("Emoji")]
        public async Task<IActionResult> Create([FromBody] PostEmoji dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            if (string.IsNullOrWhiteSpace(dto.Emoji))
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Emoji is required." });

            var result = await _reactionRepo.CreateAsync(dto);
            return Ok(result);
        }
        [HttpDelete("{id:int}")]
        public async Task<IActionResult>Delete(int id)
        {
            await _reactionRepo.DeleteAsync(id);
            return NoContent();
        }
    }
}
