using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.Helper
{
    public class PostHelper
    {
        public interface IAuditableEntity
        {
            DateTime? CreatedAt { get; set; }
            DateTime? UpdatedAt { get; set; }
        }

        public interface IAuditableUser
        {
            Guid? CreatedBy { get; set; }
            Guid? UpdatedBy { get; set; }
        }
    }
}
