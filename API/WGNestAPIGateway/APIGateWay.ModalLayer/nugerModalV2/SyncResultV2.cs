using APIGateWay.ModalLayer.nugetmodal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.nugerModalV2
{
    public class SyncResultV2
    {
        /// <summary>
        /// Module success flag
        /// </summary>
        public bool Ok { get; set; }

        /// <summary>
        /// Data payload (present only if Ok = true)
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// Error payload (present only if Ok = false)
        /// </summary>
        public SyncErrorV2 Err { get; set; }
    }
    public class SyncErrorV2
    {
        /// <summary>
        /// Error code
        /// </summary>
        public string C { get; set; }

        /// <summary>
        /// Error message
        /// </summary>
        public string M { get; set; }

        /// <summary>
        /// Retryable flag
        /// </summary>
        public bool R { get; set; }
    }

}
