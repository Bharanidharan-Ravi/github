// routesConfig.js
// export const roleRoutes = [
//       { name: 'Dashboard', path: '/dashboard' },
//       { name: 'Tickets', path: '/tickets' },
//       { name: 'Projects', path: '/projects' },   
//       { name: 'Repository', path: '/repository' },  
// ];

export const roleRoutes = {
      1: [ // Admin
            { name: 'Dashboard', path: '/dashboard' },
            { name: 'Tickets', path: '/tickets' },
            { name: 'Projects', path: '/projects' },   
            { name: 'Repository', path: '/repository' },  
      ],
      2: [ // Employee
            { name: 'Dashboard', path: '/dashboard' },
            { name: 'Tickets', path: '/tickets' },
            { name: 'Projects', path: '/projects' },   
            { name: 'Repository', path: '/repository' },  
      ],
      3: [ // Client
            { name: 'Dashboard', path: '/dashboard' },
            { name: 'Tickets', path: '/tickets' },
            { name: 'Projects', path: '/projects' },   
      ],
    };
    