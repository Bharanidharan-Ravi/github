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

        // ROLESMASTER: 1=Admin, 2=Employee (named "Manager" above), 3=Client (named
        // "Viewer" above). Role 3 is an external client login, never an employee —
        // do NOT include it here. See LOGIN_MASTER / validateuser SP.
        public static readonly int[] EmployeeOnly = { Manager };

        // Who can submit a leave request: Admin + Employee. Client (3) excluded.
        public static readonly int[] LeaveRequestCreate = { Admin, Manager };
    }
}
