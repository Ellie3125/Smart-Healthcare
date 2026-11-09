using System.Security.Claims;
using IdentityPatients.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace IdentityPatients.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var userId))
        {
            return userId;
        }
        return null;
    }

    protected IActionResult HandleResult<T>(ServiceResult<T> result)
    {
        if (!result.IsSuccess)
        {
            var problem = new ProblemDetails
            {
                Status = result.StatusCode,
                Title = result.ErrorTitle,
                Detail = result.ErrorDetail
            };

            if (result.Extensions != null)
            {
                foreach (var (key, val) in result.Extensions)
                {
                    problem.Extensions[key] = val;
                }
            }

            return StatusCode(result.StatusCode, problem);
        }

        if (result.StatusCode == 201)
        {
            return StatusCode(201, result.Value);
        }

        return Ok(result.Value);
    }

    protected IActionResult HandleResult(ServiceResult result)
    {
        if (!result.IsSuccess)
        {
            var problem = new ProblemDetails
            {
                Status = result.StatusCode,
                Title = result.ErrorTitle,
                Detail = result.ErrorDetail
            };

            if (result.Extensions != null)
            {
                foreach (var (key, val) in result.Extensions)
                {
                    problem.Extensions[key] = val;
                }
            }

            return StatusCode(result.StatusCode, problem);
        }

        if (result.StatusCode == 204)
        {
            return NoContent();
        }

        return Ok();
    }
}
