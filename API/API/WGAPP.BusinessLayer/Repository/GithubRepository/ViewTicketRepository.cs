using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.BusinessLayer.Interface.GithubInterface;
using WGAPP.DomainLayer.Interface.GithubInterface;
using WGAPP.ModelLayer.GithubModal.ViewIssues;

namespace WGAPP.BusinessLayer.Repository.GithubRepository
{
    public class ViewTicketRepository : IViewTicketRepository
    {
        private readonly IViewTicketService _viewTicketService;
        public ViewTicketRepository (IViewTicketService viewTicketService)
        {
            _viewTicketService = viewTicketService;
        }
        public async Task<ThreadbyTicketId> GetThreadData(Guid IssuesId)
        {
            var IssuesData = await _viewTicketService.GetThreadData(IssuesId);
            return IssuesData;
        }
       
        //public Tuple<List<GetIssuesModal>, List<LabelMaster>, List<ProjectMaster>> GetAllIssuesData()
        //{
        //    var issuesData = _viewTicketService.GetAllIssuesData();
        //    return issuesData;
        //}
    }
}
