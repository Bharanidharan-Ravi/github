using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.nugerModalV2
{
    public class SyncResponseV2
    {
        /// <summary>
        /// Version identifier (always 2)
        /// </summary>
        public int V { get; set; } = 2;

        /// <summary>
        /// Request id
        /// </summary>
        public string Rid { get; set; }

        /// <summary>
        /// Server time (epoch seconds)
        /// </summary>
        public long St { get; set; }

        /// <summary>
        /// Results per config key
        /// </summary>
        public Dictionary<string, SyncResultV2> Res { get; set; } = new();
    }

}
