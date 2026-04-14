using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.MasterData;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Service
{
    public class RequestStepContext : IRequestStepContext
    {
        private readonly List<ApiLogStep> _steps = new();
        private int _order = 0;
        private static DateTime GetIndiaTime()
        {
            var indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, indiaTimeZone);
        }


        // ── Start timing ──────────────────────────────────────────────────────
        public Stopwatch StartStep()
        {
            var sw = new Stopwatch();
            sw.Start();
            return sw;
        }
        // ── Record success ────────────────────────────────────────────────────
        public void Success(string tableName, string operation, string? insertedId, Stopwatch timer)
        {
            timer.Stop();
            _steps.Add(new ApiLogStep
            {
                StepOrder = ++_order,
                TableName = tableName,
                Operation = operation,
                Status = "Success",
                InsertedId = insertedId,
                DurationMs = timer.ElapsedMilliseconds,
                ErrorMessage = null,
                InnerException = null,
                CreatedAt = GetIndiaTime()
            });
        }

        // ── Record failure ────────────────────────────────────────────────────
        public void Failure(string tableName, string operation, string? errorMessage, string? innerException, Stopwatch timer)
        {
            timer.Stop();
            _steps.Add(new ApiLogStep
            {
                StepOrder = ++_order,
                TableName = tableName,
                Operation = operation,
                Status = "Failed",
                InsertedId = null,
                DurationMs = timer.ElapsedMilliseconds,
                ErrorMessage = errorMessage,
                InnerException = innerException,
                CreatedAt = GetIndiaTime()
            });
        }

        public List<ApiLogStep> GetSteps() => _steps;
    }
}
