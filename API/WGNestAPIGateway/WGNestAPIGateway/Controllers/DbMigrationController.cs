using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    /// <summary>
    /// DB snapshots and schema-only migrations of the SQL Server DB in ConnectionStrings:DefaultConnection
    /// (header X-Environment: Test uses TestConnection). Admin only, and switched off unless
    /// appsettings "DbMigration:Enabled" is true — both checked in DbMigrationService.
    /// </summary>
    [ApiController]
    [Route("api/db-migration")]
    public class DbMigrationController : ControllerBase
    {
        private readonly IDbMigrationService _service;

        public DbMigrationController(IDbMigrationService service)
        {
            _service = service;
        }

        /// <summary>Manual snapshot: structure .json (upload this to /migrate on another DB) + full .sql backup.</summary>
        [HttpPost("snapshots")]
        public async Task<IActionResult> TakeSnapshot([FromBody] CreateDbSnapshotRequest? request)
        {
            return Ok(ApiResponseHelper.Success(await _service.TakeSnapshotAsync(request ?? new CreateDbSnapshotRequest())));
        }

        /// <summary>All snapshots and migrations, newest first.</summary>
        [HttpGet("snapshots")]
        public async Task<IActionResult> List()
        {
            return Ok(ApiResponseHelper.Success(await _service.ListAsync()));
        }

        /// <summary>Downloads a snapshot's file: kind = json (structure), sql (backup) or rollback.</summary>
        [HttpGet("snapshots/{snapshotId:int}/file")]
        public async Task<IActionResult> Download(int snapshotId, [FromQuery] string kind = "json")
        {
            var record = await _service.GetAsync(snapshotId);
            var path = kind?.ToLowerInvariant() switch
            {
                "json" => record.FilePath,
                "sql" => record.SqlFilePath,
                "rollback" => record.RollbackFilePath,
                _ => null
            };
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return NotFound(ApiResponseHelper.Failure($"No {kind} file for snapshot {snapshotId}.", 404));
            return PhysicalFile(path, "application/octet-stream", System.IO.Path.GetFileName(path));
        }

        /// <summary>
        /// Upload a structure snapshot (.json) from the source DB. New tables, new columns, column
        /// type changes and new/changed procedures are applied in one transaction (no data, no drops),
        /// then a snapshot is taken. DryRun (default true) only returns the plan — send DryRun=false to apply.
        /// </summary>
        [HttpPost("migrate")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Migrate([FromForm] DbMigrateRequest request)
        {
            return Ok(ApiResponseHelper.Success(await _service.MigrateAsync(request)));
        }

        /// <summary>Undoes the latest applied migration.</summary>
        [HttpPost("migrations/{migrationId:int}/rollback")]
        public async Task<IActionResult> Rollback(int migrationId)
        {
            return Ok(ApiResponseHelper.Success(await _service.RollbackAsync(migrationId)));
        }
    }
}
