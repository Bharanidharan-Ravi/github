using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.CommonSevice
{
    public interface IDbConnectionFactory
    {
        SqlConnection CreateConnection();
    }
}
