using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModelLayer
{
    public class ErrorResponse
    {
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; }

        // Development only — full inner-exception chain and stack trace. Omitted from the JSON otherwise.
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? InnerExceptions { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? StackTrace { get; set; }
    }
}
