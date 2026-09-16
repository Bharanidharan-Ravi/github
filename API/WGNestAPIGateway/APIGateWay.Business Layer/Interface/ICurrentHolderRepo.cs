using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Interface
{
    public interface ICurrentHolderRepo
    {
        Task<List<Assignee_To_Move>> UpdateCurrentHolderAsync(Guid issueId, postAssigneeToMoveDto dto);
    }
}
