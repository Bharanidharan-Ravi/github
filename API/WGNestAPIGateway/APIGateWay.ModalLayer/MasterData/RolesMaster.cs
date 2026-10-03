using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIGateWay.ModalLayer.MasterData
{
    [Table("ROLESMASTER")]
    public class RolesMaster
    {
        [Key]
        public int ID { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public int? ParentRoleID { get; set; }
    }
}
