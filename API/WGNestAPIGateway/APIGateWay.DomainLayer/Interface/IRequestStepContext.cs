using APIGateWay.ModalLayer.MasterData;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IRequestStepContext
    {
        /// <summary>Starts a stopwatch for timing an individual table operation.</summary>
        Stopwatch StartStep();

        /// <summary>Records a successful table operation.</summary>
        void Success(string tableName, string operation, string? insertedId, Stopwatch timer);

        /// <summary>Records a failed table operation.</summary>
        void Failure(string tableName, string operation, string? errorMessage, string? innerException, Stopwatch timer);

        /// <summary>Returns all steps collected so far this request.</summary>
        List<ApiLogStep> GetSteps();
    }
}
