// // routesConfig.js
// export const roleRoutes = {
//     1: [ // Admin
//       { name: 'Repository', path: '/admin/*' },
//       { name: 'Tickets', path: '/admin/*' },
//       { name: 'Projects', path: '/admin/*' },
//     ],
//     2: [ // Employee
//       { name: 'Repository', path: '/employee/repository' },
//       { name: 'Tickets', path: '/employee/tickets' },
//       { name: 'Projects', path: '/employee/projects' },
//     ],
//     3: [ // Client
//       // { name: 'Repository', path: '/client/repository' },
//       { name: 'Tickets', path: '/client/' },
//       { name: 'Projects', path: '/client/projects' },
//     ],
//   };
  
// routesConfig.js
export const roleRoutes = {
  1: [ // Admin
    { name: 'Admin', path: '/admin/tickets', mfe: 'tickets' },
    { name: 'Admin', path: '/admin/tickets/create', mfe: 'tickets' },
    { name: 'Projects', path: '/admin/projects', mfe: 'tickets' },
    { name: 'Repository', path: '/admin/repository', mfe: 'tickets' },
  ],
  2: [ // Employee
    { name: 'Employee', path: '/employee/tickets', mfe: 'tickets' },
    { name: 'Dashboard', path: '/employee/dashboard', mfe: 'tickets' },
    { name: 'Employee', path: '/employee/tickets/create', mfe: 'tickets' },
    { name: 'Employee', path: '/employee/tickets/:ticketId', mfe: 'tickets' },
    { name: 'Projects', path: '/employee/projects', mfe: 'tickets' },
    { name: 'Projects', path: '/employee/projects/create', mfe: 'tickets' },
    { name: 'Projects', path: '/employee/projects/:projId', mfe: 'tickets' },
    { name: 'Repository', path: '/employee/repository', mfe: 'tickets'},
    { name: 'Repository', path: '/employee/repository/create', mfe: 'tickets'},
    { name: 'Repository', path: '/employee/repository/:repoId', mfe: 'tickets'},
  ],
  3: [ // Client
    { name: 'Client', path: '/client/tickets', mfe: 'tickets' },
    { name: 'Client', path: '/client/tickets/create', mfe: 'tickets' },
    { name: 'Client', path: '/Client/tickets/:ticketId', mfe: 'tickets' },
    { name: 'Client', path: '/Client/repository', mfe: 'tickets'},
    { name: 'Client', path: '/client/dashboard', mfe: 'tickets' },
    { name: 'Client', path: '/client/projects', mfe: 'tickets' },
    // { name: 'Repository', path: '/client/repository' },
  ],
};
