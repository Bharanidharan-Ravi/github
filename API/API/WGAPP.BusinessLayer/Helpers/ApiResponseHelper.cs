using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.ModelLayer;

namespace WGAPP.BusinessLayer.Helpers
{
    public static class ApiResponseHelper
    {
        public static ApiResponse<T> Success<T>(T data, string message = "Success")
        {
            return new ApiResponse<T>
            {
                Code = 200,
                Message = message,
                Data = data
            };
        }

        public static ApiResponse<object> Failure(string message, int statusCode = 400)
        {
            return new ApiResponse<object>
            {
                Code = statusCode,
                Message = message,
                Data = null
            };
        }
    }
}
