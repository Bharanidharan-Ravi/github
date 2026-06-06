using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.PostHelper;

namespace APIGateWay.ModalLayer.MasterData
{
    public class AppSetting : IHasUpdatedAt
    {
        public Guid Id { get; set; }

        public string SettingKey { get; set; } = string.Empty;

        public string SettingValue { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
