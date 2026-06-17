using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;

namespace Reporting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    /// <summary>تحويل Result إلى استجابة HTTP موحّدة مع ربط رموز الأخطاء بأكواد الحالة.</summary>
    protected IActionResult FromResult(Result result) =>
        result.Succeeded ? Ok() : ToProblem(result);

    protected IActionResult FromResult<T>(Result<T> result) =>
        result.Succeeded ? Ok(result.Value) : ToProblem(result);

    protected IActionResult ToProblem(Result result)
    {
        var status = result.ErrorCode switch
        {
            "auth.unauthenticated" => StatusCodes.Status401Unauthorized,
            "auth.invalid_credentials" => StatusCodes.Status401Unauthorized,
            "auth.invalid_refresh" => StatusCodes.Status401Unauthorized,
            "auth.account_disabled" => StatusCodes.Status403Forbidden,
            "auth.forbidden" => StatusCodes.Status403Forbidden,
            "auth.not_found" => StatusCodes.Status404NotFound,
            _ when result.ErrorCode?.EndsWith(".not_found") == true => StatusCodes.Status404NotFound,
            _ when result.ErrorCode?.EndsWith(".conflict") == true => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return Problem(detail: result.Error, statusCode: status, type: result.ErrorCode);
    }
}
