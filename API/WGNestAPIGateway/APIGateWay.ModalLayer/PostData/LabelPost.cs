using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    public class CreateLabelDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        // Hex color code — e.g. "#FF5733"
        [System.ComponentModel.DataAnnotations.MaxLength(20)]
        public string? Color { get; set; }
    }

    // ── PUT /api/label/{id} ────────────────────────────────────────────────
    // Full update — Title, Description, Color, Status all in one call
    public class UpdateLabelDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(20)]
        public string? Color { get; set; }

        // Status optional in full update — pass to change in same call
        [System.ComponentModel.DataAnnotations.MaxLength(100)]
        public string? Status { get; set; }
    }

    // ── PATCH /api/label/{id}/status ──────────────────────────────────────
    // Status-only update — Body: { "Status": "Inactive" }
    public class UpdateLabelStatusDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MaxLength(100)]
        public string Status { get; set; } = string.Empty;
    }
}
