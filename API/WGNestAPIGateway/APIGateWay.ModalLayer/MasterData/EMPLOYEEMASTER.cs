using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.PostHelper;

namespace APIGateWay.ModalLayer.MasterData
{
    [Table("EMPLOYEEMASTER")]
    public class EMPLOYEEMASTER : IAuditableEntity, IAuditableUser
    {
        [Key]
        public Guid EmployeeID { get; set; }

        [Required, MaxLength(100)]
        public string EmployeeName { get; set; }

        [MaxLength(100)]
        public int? Team { get; set; }

        [MaxLength(50)]
        public int? Role { get; set; }

        [MaxLength(100)]
        public string? Specialization { get; set; }
        public DateOnly? DoB { get; set; }

        [MaxLength(10)]
        public string Status { get; set; } = "Active"; // default value

        public DateTime? CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [MaxLength(50)]
        public string? EmployeeCode { get; set; }

        [ForeignKey(nameof(EmployeeID))]
        public virtual LOGIN_MASTER Login { get; set; }
    }
}
