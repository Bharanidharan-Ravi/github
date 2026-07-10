using System.Text.Json;
using System.Text.Json.Serialization;
using APIGateWay.BusinessLayer.Helpers;
using static APIGateWay.ModelLayer.ErrorException.Exceptionlist;

namespace APIGateWay.Middelware
{
    public class ResponseWrappingMiddleware
    {
        private readonly RequestDelegate _next;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            //PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            //DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        public ResponseWrappingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var originalBodyStream = context.Response.Body;

            using var memoryStream = new MemoryStream();
            context.Response.Body = memoryStream;

            await _next(context);

            memoryStream.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();

            // Restore original stream so we can write the wrapped response to it
            context.Response.Body = originalBodyStream;

            // Skip wrapping for Swagger/OpenAPI endpoints
            if (context.Request.Path.StartsWithSegments("/swagger") ||
                context.Request.Path.StartsWithSegments("/swagger/index.html") ||
                context.Request.Path.StartsWithSegments("/swagger/v1/swagger.json"))
            {
                await context.Response.WriteAsync(responseBody);
                return;
            }

            // Only wrap successful responses with non-empty body and content type set
            if (context.Response.StatusCode == 200 &&
                !string.IsNullOrEmpty(responseBody) &&
                context.Response.ContentType != null)
            {
                if (context.Response.ContentType.Contains("application/json"))
                {
                    // Try to detect if already wrapped
                    bool isAlreadyWrapped = false;
                    try
                    {
                        var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(responseBody, JsonOptions);
                        if (apiResponse != null && apiResponse.Message != null)
                        {
                            isAlreadyWrapped = true;
                        }
                    }
                    catch
                    {
                        // ignored - treat as not wrapped
                    }

                    if (isAlreadyWrapped)
                    {
                        // Write original response body back as is
                        await context.Response.WriteAsync(responseBody);
                        return;
                    }

                    // Parse JSON to decide if it is string or object
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);

                    string message = "Success";
                    object? data = null;

                    if (jsonElement.ValueKind == JsonValueKind.String)
                    {
                        // Simple string result (e.g. "Treatment updated successfully.")
                        message = jsonElement.GetString() ?? "Success";
                        data = null;
                    }
                    else
                    {
                        // Complex JSON object or array
                        data = jsonElement;
                    }

                    var wrappedResponse = JsonSerializer.Serialize(new ApiResponse<object>
                    {
                        Code = 200,
                        Message = message,
                        Data = data
                    }, JsonOptions);

                    context.Response.ContentLength = null; // Reset content length
                    context.Response.ContentType = "application/json";

                    await context.Response.WriteAsync(wrappedResponse);
                    return;
                }
                else if (context.Response.ContentType.Contains("text/plain", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Wrap plain text response as message with no data
                    var wrappedResponse = JsonSerializer.Serialize(new ApiResponse<object>
                    {
                        Code = 200,
                        Message = responseBody,
                        Data = null
                    }, JsonOptions);

                    context.Response.ContentLength = null;
                    context.Response.ContentType = "application/json";

                    await context.Response.WriteAsync(wrappedResponse);
                    return;
                }
            }

            // For error responses or other content types, write original response body back
            await context.Response.WriteAsync(responseBody);
        }
    }
}

//        public ResponseWrappingMiddleware(RequestDelegate next)
//        {
//            _next = next;
//        }



//        public async Task InvokeAsync(HttpContext context)
//        {
//            var originalBodyStream = context.Response.Body;

//            using var memoryStream = new MemoryStream();
//            context.Response.Body = memoryStream;

//            await _next(context);

//            memoryStream.Seek(0, SeekOrigin.Begin);
//            var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();

//            // Restore original stream so we can write the wrapped response to it
//            context.Response.Body = originalBodyStream;

//            // Skip wrapping for Swagger/OpenAPI endpoints
//            if (context.Request.Path.StartsWithSegments("/swagger") ||
//                context.Request.Path.StartsWithSegments("/swagger/index.html") ||
//                context.Request.Path.StartsWithSegments("/swagger/v1/swagger.json"))
//            {
//                await context.Response.WriteAsync(responseBody);
//                return;
//            }

//            // Only wrap successful responses with non-empty body and content type set
//            if (context.Response.StatusCode == 200 &&
//                !string.IsNullOrEmpty(responseBody) &&
//                context.Response.ContentType != null)
//            {
//                if (context.Response.ContentType.Contains("application/json"))
//                {
//                    // Try to detect if already wrapped
//                    bool isAlreadyWrapped = false;
//                    try
//                    {
//                        var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(responseBody);
//                        if (apiResponse != null && apiResponse.Message != null)
//                        {
//                            isAlreadyWrapped = true;
//                        }
//                    }
//                    catch
//                    {
//                        // ignored - treat as not wrapped
//                    }

//                    if (isAlreadyWrapped)
//                    {
//                        // Write original response body back as is
//                        await context.Response.WriteAsync(responseBody);
//                        return;
//                    }

//                    // Parse JSON to decide if it is string or object
//                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(responseBody);

//                    string message = "Success";
//                    object? Data = null;

//                    if (jsonElement.ValueKind == JsonValueKind.String)
//                    {
//                        // Simple string result (e.g. "Treatment updated successfully.")
//                        message = jsonElement.GetString() ?? "Success";
//                        Data = null;
//                    }
//                    else
//                    {
//                        // Complex JSON object or array
//                        Data = jsonElement;
//                    }

//                    var wrappedResponse = JsonSerializer.Serialize(new ApiResponse<object>
//                    {
//                        Code = 200,
//                        Message = message,
//                        Data = Data
//                    });

//                    context.Response.ContentLength = null; // Reset content length
//                    context.Response.ContentType = "application/json";

//                    await context.Response.WriteAsync(wrappedResponse);
//                    return;
//                }
//                else if (context.Response.ContentType.Contains("text/plain", StringComparison.OrdinalIgnoreCase))
//                {
//                    // Wrap plain text response as message with no data
//                    var wrappedResponse = JsonSerializer.Serialize(new ApiResponse<object>
//                    {
//                        Code = 200,
//                        Message = responseBody,
//                        Data = null
//                    });

//                    context.Response.ContentLength = null;
//                    context.Response.ContentType = "application/json";

//                    await context.Response.WriteAsync(wrappedResponse);
//                    return;
//                }
//            }

//            // For error responses or other content types, write original response body back
//            await context.Response.WriteAsync(responseBody);
//        }
//    }
//}



//        public async Task InvokeAsync(HttpContext context)
//        {
//            var originalBodyStream = context.Response.Body;

//            using var memoryStream = new MemoryStream();
//            context.Response.Body = memoryStream;

//            await _next(context);

//            memoryStream.Seek(0, SeekOrigin.Begin);
//            var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();

//            // Restore original stream to write back
//            context.Response.Body = originalBodyStream;

//            // Skip wrapping for Swagger/OpenAPI endpoints
//            if (context.Request.Path.StartsWithSegments("/swagger") ||
//                context.Request.Path.StartsWithSegments("/swagger/index.html") ||
//                context.Request.Path.StartsWithSegments("/swagger/v1/swagger.json"))
//            {
//                await context.Response.WriteAsync(responseBody);
//                return;
//            }

//            // Wrap only successful JSON responses

//            if (context.Response.StatusCode == 200 &&
//    !string.IsNullOrEmpty(responseBody) &&
//    context.Response.ContentType != null)
//            {
//                string message = "Success";
//                object? data = null;

//                if (context.Response.ContentType.Contains("application/json"))
//                {
//                    var parsed = JsonSerializer.Deserialize<object>(responseBody);

//                    // if parsed is a string => use it as message
//                    if (parsed is JsonElement element && element.ValueKind == JsonValueKind.String)
//                    {
//                        message = element.GetString() ?? "Success";
//                        data = null;
//                    }
//                    else
//                    {
//                        message = "Success";
//                        data = parsed;
//                    }
//                }
//                else if (context.Response.ContentType.Contains("text/plain", StringComparison.OrdinalIgnoreCase))
//                {
//                    message = responseBody; // plain string message
//                    data = null;
//                }

//                var wrappedResponse = JsonSerializer.Serialize(new ApiResponse<object>
//                {
//                    Code = 200,
//                    Message = message,
//                    Data = data
//                });

//                await context.Response.WriteAsync(wrappedResponse);
//                return;
//            }
//            //    if (context.Response.StatusCode == 200 &&
//            //        !string.IsNullOrEmpty(responseBody) &&
//            //        context.Response.ContentType != null &&
//            //        context.Response.ContentType.Contains("application/json"))
//            //    {
//            //        var wrappedResponse = JsonSerializer.Serialize(new ApiResponse<object>
//            //        {
//            //            Code = 200,
//            //            Message = "Success",
//            //            Data = JsonSerializer.Deserialize<object>(responseBody)
//            //        });

//            //        await context.Response.WriteAsync(wrappedResponse);
//            //    }
//            //    else if (context.Response.StatusCode == 200 &&
//            //        !string.IsNullOrEmpty(responseBody) &&
//            //        context.Response.ContentType != null &&
//            //        context.Response.ContentType.Contains("text/plain", StringComparison.OrdinalIgnoreCase))
//            //    {
//            //        var wrappedResponse = JsonSerializer.Serialize(new ApiResponse<object>
//            //        {
//            //            Code = 200,
//            //            Message = "Success",
//            //            Data = responseBody
//            //        });

//            //        await context.Response.WriteAsync(wrappedResponse);
//            //    }
//            //    else
//            //    {
//            //        // For errors or non-JSON responses, just write the original body back
//            //        await context.Response.WriteAsync(responseBody);
//            //    }
//            //}


//            //public async Task InvokeAsync(HttpContext context)
//            //{
//            //    // Temporarily capture the original response stream
//            //    var originalBodyStream = context.Response.Body;

//            //    using var memoryStream = new MemoryStream();
//            //    context.Response.Body = memoryStream;

//            //    await _next(context);
//            //    // Exclude swagger/OpenAPI endpoints from wrapping
//            //    if (context.Request.Path.StartsWithSegments("/swagger") ||
//            //        context.Request.Path.StartsWithSegments("/api-docs") ||
//            //        context.Request.Path.StartsWithSegments("/swagger/v1/swagger.json"))
//            //    {
//            //        memoryStream.Seek(0, SeekOrigin.Begin);
//            //        await memoryStream.CopyToAsync(originalBodyStream);
//            //        context.Response.Body = originalBodyStream;
//            //        return;
//            //    }

//            //    memoryStream.Seek(0, SeekOrigin.Begin);
//            //    var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();

//            //    // Restore the original stream so we can write to it
//            //    context.Response.Body = originalBodyStream;

//            //    // Only wrap successful JSON responses
//            //    if (context.Response.StatusCode == 200 &&
//            //        context.Response.ContentType != null &&
//            //        context.Response.ContentType.Contains("application/json"))
//            //    {
//            //        memoryStream.Seek(0, SeekOrigin.Begin);
//            //        var originalResponse = await new StreamReader(memoryStream).ReadToEndAsync();

//            //        var wrappedResponse = JsonSerializer.Serialize(new ApiResponse<object>
//            //        {
//            //            Code = 200,
//            //            Message = "Success",
//            //            Data = JsonSerializer.Deserialize<object>(originalResponse)
//            //        });

//            //        context.Response.Body = originalBodyStream;
//            //        await context.Response.WriteAsync(wrappedResponse);
//            //    }
//            //    else
//            //    {
//            //        // For non-JSON or non-200 responses, pass through as is
//            //        //memoryStream.Seek(0, SeekOrigin.Begin);
//            //        //await memoryStream.CopyToAsync(originalBodyStream);
//            //        memoryStream.Seek(0, SeekOrigin.Begin);
//            //        await memoryStream.CopyToAsync(originalBodyStream);
//            //        context.Response.Body = originalBodyStream;
//            //    }
//            //}
//        }
//    }
//}