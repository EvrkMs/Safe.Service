using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Safe.Application.Services;
using Safe.Domain.DTOs;
using Safe.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using static Safe.Domain.Commands.SafeCommand;
using FluentValidationException = FluentValidation.ValidationException; // <-- добавь этот алиас

namespace Safe.Host.Controllers;

[ApiController]
[Route("api/safe")]
public sealed class SafeController(ISafeService service, ILogger<SafeController> logger) : ControllerBase
{
    [HttpPost("changes")]
    [ProducesResponseType(typeof(CreateChangeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [Authorize(Policy = "Safe.Write")]
    public async Task<IActionResult> Create([FromBody] CreateChangeRequest body, CancellationToken ct)
    {
        try
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
        catch (FluentValidationException ex) // <-- используй алиас
        {
            foreach (var error in ex.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            return ValidationProblem(ModelState);
        }
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
    [ProducesResponseType(typeof(Page<SafeChangeDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = "Safe.Read")]
    public async Task<IActionResult> GetList([FromQuery] ChangesQuery query, CancellationToken ct)
    {
        var (items, total) = await service.GetChangesAsync(new(
            From: query.From,
            To: query.To,
            Status: query.Status,
            Page: query.Page,
            PageSize: query.PageSize
        ), ct);

        var pageSize = Math.Clamp(query.PageSize ?? 50, 1, 500);
        var page = Math.Max(query.Page ?? 1, 1);

        return Ok(new Page<SafeChangeDto>(items, total, page, pageSize));
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
        try
        {
            await service.ReverseAsync(new(id, body.Comment), ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning(ex, "SafeChange {Id} not found for reversal", id);
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Cannot reverse SafeChange {Id}: {Message}", id, ex.Message);
            return Conflict(new ProblemDetails
            {
                Title = "Invalid state",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Concurrency conflict reversing SafeChange {Id}", id);
            return Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "Запись была изменена другим пользователем. Обновите данные и попробуйте снова.",
                Status = StatusCodes.Status409Conflict
            });
        }
    }
}

/* Request/Response модели */

public sealed record CreateChangeRequest(
    [Required] SafeChangeReason Reason,
    SafeChangeDirection? Direction,
    [Required, Range(0.01, 1_000_000_000)] decimal Amount,
    [Required, StringLength(64, MinimumLength = 1)] string Category,
    [Required, StringLength(512, MinimumLength = 1)] string Comment,
    DateTimeOffset? OccurredAt);

public sealed record CreateChangeResponse(long Id);

public sealed record ReverseRequest(
    [Required, StringLength(512, MinimumLength = 1)] string Comment);

public sealed record ChangesQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    SafeChangeStatus? Status,
    int? Page,
    int? PageSize);

public sealed record Page<T>(
    IReadOnlyList<T> Items,
    int Total,
    int PageNumber,
    int PageSize);