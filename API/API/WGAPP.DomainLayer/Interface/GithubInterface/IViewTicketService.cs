using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.ModelLayer.GithubModal.ViewIssues;

namespace WGAPP.DomainLayer.Interface.GithubInterface
{
    public interface IViewTicketService
    {
        Task<ThreadbyTicketId> GetThreadData(Guid IssuesId);
        //Task<Attachment> PostAttachment(Attachment attachment);
        //Tuple<List<GetIssuesModal>, List<LabelMaster>, List<ProjectMaster>> GetAllIssuesData();
    }
}
