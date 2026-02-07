using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    [Table("EMPLOYEEMASTER")]
    public class EMPLOYEEMASTER
    {
        [Key]
        public Guid EmployeeID { get; set; }

        [Required, MaxLength(100)]
        public string EmployeeName { get; set; }

        [MaxLength(100)]
        public string Team { get; set; }

        [MaxLength(50)]
        public int Role { get; set; }

        [MaxLength(100)]
        public string Specialization { get; set; }

        [MaxLength(10)]
        public string Status { get; set; } = "Active"; // default value

        [Required, MaxLength(50)]
        public string CreatedBy { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.Now;

        [MaxLength(50)]
        public string UpdatedBy { get; set; }

        public DateTime? UpdatedOn { get; set; }

        [MaxLength(100)]
        public string Email { get; set; }

        [MaxLength(20)]
        public string PhoneNumber { get; set; }

        [MaxLength(50)]
        public string EmployeeCode { get; set; }

        [ForeignKey(nameof(EmployeeID))]
        public virtual LOGIN_MASTER Login { get; set; }
    }
}
