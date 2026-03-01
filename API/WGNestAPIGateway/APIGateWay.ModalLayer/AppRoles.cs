using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer
{
    public static class AppRoles
    {
        public const int Admin = 1;  // Master admin  — full access, zero additional checks
        public const int Manager = 2;  // Manager       — all views scoped to their repos, no repo creation
        public const int Viewer = 3;  // Viewer        — project + ticket only, scoped to their repos

        public static readonly int[] All = { Admin, Manager, Viewer };
        public static readonly int[] AdminManager = { Admin, Manager };
        public static readonly int[] AdminOnly = { Admin };
    }
}
