using Azure.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class SyncRequest
    {
        public DateTime? ProjectLastSync { get; set; }
        public DateTime? RepoLastSync { get; set; }
        public DateTime? EmpLastSync { get; set; }
    }
}
