using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.ModelLayer.GithubModal.ViewIssues;

namespace WGAPP.BusinessLayer.Interface.GithubInterface
{
    public interface IViewTicketRepository
    {
        Task<ThreadbyTicketId> GetThreadData(Guid IssuesId);
        //Task<Attachment> InsertAttachment(AttachmentDTo dto);
        //Tuple<List<GetIssuesModal>, List<LabelMaster>, List<ProjectMaster>> GetAllIssuesData();
    }
}
