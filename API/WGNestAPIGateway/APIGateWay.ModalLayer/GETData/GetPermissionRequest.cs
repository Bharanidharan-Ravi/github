using System;

namespace APIGateWay.ModalLayer.GETData
{
    // Maps 1:1 to usp_GetPermissionRequests output (scripts/permission_request_migration.sql).
    public class GetPermissionRequest
    {
        public Guid ID { get; set; }
        public Guid EMPLOYEE_ID { get; set; }
        public string? EmployeeName { get; set; }
        public DateTime PERMISSION_DATE { get; set; }
        public int DURATION_MINUTES { get; set; }
        public string? REMARKS { get; set; }
        public string STATUS { get; set; }
        public DateTime REQUESTED_DATE { get; set; }
        public Guid? APPROVED_BY { get; set; }
        public DateTime? APPROVED_DATE { get; set; }
        public string? REJECT_REASON { get; set; }
        public Guid? REJECTED_BY { get; set; }
        public DateTime? REJECTED_DATE { get; set; }
        public Guid CREATED_BY { get; set; }
        public DateTime CREATED_DATE { get; set; }
        public Guid? UPDATED_BY { get; set; }
        public DateTime? UPDATED_DATE { get; set; }
        public int? ACTUAL_DURATION_MINUTES { get; set; }
        public Guid? ACTUAL_DURATION_BY { get; set; }
        public DateTime? ACTUAL_DURATION_DATE { get; set; }
    }
}
