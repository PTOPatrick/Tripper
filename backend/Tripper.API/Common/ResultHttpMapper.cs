using Tripper.Application.Common;

namespace Tripper.API.Common;

public static class ResultHttpMapper
{
    public static IResult ToHttpResult(this Result result)
    {
        return result.IsSuccess ? Results.Ok() : MapError(result.Error!);
    }

    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error!);
    }

    private static IResult MapError(Error e) => e.Type switch
    {
        ErrorType.Validation => Results.BadRequest(new { e.Code, e.Message }),
        ErrorType.Conflict => Results.Conflict(new { e.Code, e.Message }),
        ErrorType.Unauthorized => Results.Unauthorized(),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.NotFound => Results.NotFound(new { e.Code, e.Message }),
        _ => Results.Problem(statusCode: 500, title: e.Code, detail: e.Message)
    };
}