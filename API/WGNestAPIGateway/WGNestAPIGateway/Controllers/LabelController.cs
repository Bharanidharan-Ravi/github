using APIGateWay.Business_Layer.Interface;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/label")]
    public class LabelController : ControllerBase
    {
        private readonly ILabelRepo _labelRepo;

        public LabelController(ILabelRepo labelRepo)
        {
            _labelRepo = labelRepo;
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/label
        // Role 1 only. Creates a new label.
        // Body: { "Title": "Bug", "Description": "...", "Color": "#FF5733" }
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> CreateLabel([FromBody] CreateLabelDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Title is required." });

            var result = await _labelRepo.CreateLabelAsync(dto);
            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PUT /api/label/{id}
        // Role 1 only. Full update — Title, Description, Color, Status.
        // What never changes: Id, Created_On, Created_By
        // What auto-sets: Updated_On, Updated_By (set in LabelRepo from loginContext)
        // ─────────────────────────────────────────────────────────────────────
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateLabel(int id, [FromBody] UpdateLabelDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Title is required." });

            var result = await _labelRepo.UpdateLabelAsync(id, dto);
            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PATCH /api/label/{id}/status
        // Role 1 only. Status-only update.
        // Body: { "Status": "Inactive" }
        // Only Status + Updated_On + Updated_By change — nothing else in the row
        // ─────────────────────────────────────────────────────────────────────
        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateLabelStatus(int id, [FromBody] UpdateLabelStatusDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Status))
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Status is required." });

            var result = await _labelRepo.UpdateLabelStatusAsync(id, dto);
            return Ok(result);
        }
    }
}
