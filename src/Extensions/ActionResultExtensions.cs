using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Alejof.Notes.Extensions
{
    public static class ActionResultExtensions
    {
        public static async Task<IActionResult> AsIActionResult<TResult>(this Task<TResult> resultTask, Func<TResult, IActionResult> resultMapper = null)
        {
            var result = await resultTask;

            if (result != default)
            {
                if (resultMapper != null)
                    return resultMapper(result);

                return new OkObjectResult(result);
            }

            return new OkResult();
        }
        
        public static async Task<IActionResult> AsIActionResult<TResult>(this Task<(TResult result, UnauthorizedResult unauthorized)> tupleTask, Func<TResult, IActionResult> resultMapper = null)
        {
            var (result, unauthorized) = await tupleTask;
            
            if (unauthorized != null)
                return unauthorized;

            return await Task.FromResult(result).AsIActionResult(resultMapper);
        }
    }
}
