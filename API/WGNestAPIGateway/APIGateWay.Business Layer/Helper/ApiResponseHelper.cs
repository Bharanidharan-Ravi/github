using System;
using static APIGateWay.ModelLayer.ErrorException.Exceptionlist;

namespace APIGateWay.BusinessLayer.Helpers
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
