using APIGateWay.ModalLayer.MasterData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IApiLoggerService
    {
        /// <summary>
        /// Writes the main HTTP request log + all step rows collected by IRequestStepContext.
        /// Called by RequestLoggingMiddleware at the end of every request.
        /// </summary>
        Task WriteAsync(ApiLog log, List<ApiLogStep> steps);

        /// <summary>
        /// Writes a SignalR hub message log entry.
        /// Called directly from your Hub methods.
        /// </summary>
        Task WriteSignalRAsync(string hubMethod, string? payload, string? userId, string? userName,
                               string? errorMessage = null, string? innerException = null);
    }
}
