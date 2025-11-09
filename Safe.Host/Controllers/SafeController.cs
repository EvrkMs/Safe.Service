using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Safe.Application.Services;
using Safe.Domain.DTOs;
using Safe.Domain.Entities;
using Safe.Host.Contracts;
using static Safe.Domain.Commands.SafeCommand;

namespace Safe.Host.Controllers;

[ApiController]
[Route("api/safe")]
public sealed class SafeController(ISafeService service) : ControllerBase
{
    [HttpPost("changes")]
    [ProducesResponseType(typeof(CreateChangeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [Authorize(Policy = "Safe.Write")]
    public async Task<IActionResult> Create([FromBody] CreateChangeRequest body, CancellationToken ct)
    {
        var id = await service.CreateAsync(new(
            Reason: body.Reason,
            Direction: body.Direction,
            Amount: body.Amount,
            Category: body.Category,
            Comment: body.Comment,
            OccurredAt: body.OccurredAt
        ), ct);

        var resp = new CreateChangeResponse(id);
        return CreatedAtAction(nameof(GetById), new { id }, resp);
    }

    [HttpGet("changes/{id:long}")]
    [ProducesResponseType(typeof(SafeChangeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = "Safe.Read")]
    public async Task<IActionResult> GetById([FromRoute] long id, CancellationToken ct)
    {
        var item = await service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("changes")]
    [ProducesResponseType(typeof(PageResponse<SafeChangeDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "Safe.Read")]
    public async Task<IActionResult> GetList([FromQuery] ChangesQuery query, CancellationToken ct)
    {
        var pageResult = await service.GetChangesAsync(new(
            From: query.From,
            To: query.To,
            Status: query.Status,
            Page: query.Page,
            PageSize: query.PageSize
        ), ct);

        return Ok(PageResponse<SafeChangeDto>.From(pageResult));
    }

    [HttpGet("balance")]
    [ProducesResponseType(typeof(BalanceDto), StatusCodes.Status200OK)]
    [Authorize(Policy = "Safe.Read")]
    public async Task<IActionResult> GetBalance(CancellationToken ct)
        => Ok(await service.GetBalanceAsync(ct));

    [HttpPost("changes/{id:long}/reverse")]
    [Authorize(Policy = "Safe.Write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reverse([FromRoute] long id, [FromBody] ReverseRequest body, CancellationToken ct)
    {
        await service.ReverseAsync(new(id, body.Comment), ct);
        return NoContent();
    }
}
