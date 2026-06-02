using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.Helper
{
    public class PostHelper
    {
        public interface IHasCreatedAt
        {
            DateTime? CreatedAt { get; set; }
        }

        // 2. New interface just for updates
        public interface IHasUpdatedAt
        {
            DateTime? UpdatedAt { get; set; }
        }
        public interface IHasLastSeen
        {
            DateTime? LastSeenAt { get; set; }
        }

        // 3. Keep the old one exactly the same for older models!
        public interface IAuditableEntity : IHasCreatedAt, IHasUpdatedAt
        {
        }

        public interface IAuditableUser
        {
            Guid? CreatedBy { get; set; }
            Guid? UpdatedBy { get; set; }
        }
    }
}
