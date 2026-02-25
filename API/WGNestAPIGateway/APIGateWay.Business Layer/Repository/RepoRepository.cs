using APIGateWay.BusinessLayer.Interface;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.Hub;
using Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Repository
{
    public class RepoRepository : IRepoRepository
    {
        private readonly IRepoService _repoService;
        private readonly IRealtimeNotifier _realtimeNotifier;
        public RepoRepository(IRepoService repoService, IRealtimeNotifier realtimeNotifier)
        {
            _repoService = repoService;
            _realtimeNotifier = realtimeNotifier;
        }

        public async Task<string> PostRepo(PostRepoDto repo)
        {
            var response = await _repoService.PostRepo(repo);
            //     var response = new
            //     {
            //         Repo_Id = Guid.NewGuid().ToString(),
            //         RepoKey = "R7122",
            //         Title = "Test Repo2",
            //         Description = "<p>werwerew</p>",
            //         Status = "Active",
            //         CreatedAt = DateTime.UtcNow,
            //         CreatedBy = Guid.NewGuid().ToString(),
            //         UpdatedAt = (DateTime?)null,
            //         UpdatedBy = (Guid?)null,
            //         OwnerName = (string)null,
            //         RepoUserList = "[{\"UserName\":\"23ewrw\",\"PhoneNumber\":\"1234567890\",\"MailId\":\"er\",\"Status\":\"Active\"}]",
            //         RepoUsers = new[]
            //{
            //     new {
            //         UserName = "23ewrw",
            //         PhoneNumber = "1234567890",
            //         MailId = "er",
            //         Status = "Active"
            //     }
            // }
            //     };
            await _realtimeNotifier.BroadcastAsync(
                new RealtimeMessage
                {
                    Entity = "RepoList",
                    Action = "Create",
                    Payload = response,
                    KeyField = "repo_Id",
                    RepoKey = response.RepoKey,
                    Timestamp = DateTime.UtcNow
                }
            );
            return "Sucess";
        }

        //public async Task<string> PostRepo(PostRepoDto repo)
        //{
        //    string repoIdString = "1d947108-2043-45d5-8c4b-434010ae3d76";
        //    Guid repoIdGuid = Guid.Parse(repoIdString);
        //    await _realtimeNotifier.BroadcastAsync(
        //     new RealtimeMessage
        //     {
        //         Entity = "Repo",
        //         Action = "Create",
        //         Payload = "succe",
        //         RepoKey = "R29",
        //         Timestamp = DateTime.UtcNow
        //     }
        // );
        //    return "Sucess";
        //}
    }
}
