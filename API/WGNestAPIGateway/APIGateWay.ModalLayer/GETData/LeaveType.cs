using System.ComponentModel.DataAnnotations;

namespace APIGateWay.ModalLayer.GETData
{
    // Maps 1:1 to WG_LEAVETYPE output (SAP-backed leave type master).
    public class LeaveType
    {
        [Key]
        public string code { get; set; }
        public string Name { get; set; }
    }
}
