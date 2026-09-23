using System;

namespace APIGateWay.ModalLayer.GETData
{
    // Maps 1:1 to usp_GetLeaveRequests output (scripts/leave_request_migration.sql).
    public class GetLeaveRequest
    {
        public Guid ID { get; set; }
        public Guid EMPLOYEE_ID { get; set; }
        public string? EmployeeName { get; set; }
        public DateTime LEAVE_FROM { get; set; }
        public DateTime LEAVE_TO { get; set; }
        public int LEAVE_TYPE_ID { get; set; }
        public int NO_OF_LEAVE_DAYS { get; set; }
        public string? COMMENTS { get; set; }
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
    }
}
