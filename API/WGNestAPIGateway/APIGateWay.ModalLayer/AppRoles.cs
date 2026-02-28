using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer
{
    public static class AppRoles
    {
        public const int Admin = 1;   // Master admin — full access
        public const int Manager = 2;   // All pages, view-only on labels/employees
        public const int Viewer = 3;   // Only their own repos, tickets, projects

        public static readonly int[] All = { Admin, Manager, Viewer };
        public static readonly int[] AdminManager = { Admin, Manager };
        public static readonly int[] AdminOnly = { Admin };
    }
}
