using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WGAPP.ModelLayer
{
    public class ErrorResponse
    {
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
    }
    #region Data Response 
    public class ApiResponse<T>
    {
        public int Code { get; set; } = 200;
        public string Message { get; set; } = "Success";
        public T Data { get; set; }

        public ApiResponse() { }

        public ApiResponse(T data)
        {
            Data = data;
        }
    }
    #endregion
}
