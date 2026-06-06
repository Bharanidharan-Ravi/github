using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Utilities
{
    public class GenerateHelper
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public GenerateHelper(IHttpContextAccessor httpContext) 
        {
            _httpContextAccessor = httpContext;
        }

        public string GeneratePreviewUrl(string filePath)
        {
            // Define the base URL that corresponds to where your images are publicly accessible
            var request = _httpContextAccessor.HttpContext.Request;
            string baseUrl = $"{request.Scheme}://{request.Host}";
            string fileName = Path.GetFileName(filePath);

            var publicUrl = $"{baseUrl}/Uploads/{filePath}";

            // Return both URL and MIME type as a tuple
            return publicUrl;
        }

    }
}
