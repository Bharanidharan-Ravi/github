using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.nugetmodal
{
    public class RawSyncResult
    {
        public bool Ok { get; set; }
        public object Data { get; set; }

        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public bool Retryable { get; set; }
        public string Source { get; set; }
    }

}
