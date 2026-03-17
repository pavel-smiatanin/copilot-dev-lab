using MediatR;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Adapter.Api.Model;

namespace UrlShortener.Adapter.Api.Controllers;

[ApiController]
[Route(_controllerRoute)]
public sealed class LinksController : ControllerBase
{
    private const string _controllerRoute = "api/v1/links";

    private readonly IMediator _mediator;

    public LinksController(IMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        _mediator = mediator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateLinkResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateLink(
        [FromBody] CreateLinkRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request.ToCommand(), cancellationToken);

        string shortUrl = $"{Request.Scheme}://{Request.Host}/{result.Alias}";
        string location = $"{_controllerRoute}/{result.Id}/stats";

        CreateLinkResponse response = new(
            result.Id,
            result.Alias,
            shortUrl,
            result.DestinationUrl,
            result.ExpiresAt,
            result.CreatedAt,
            result.HasPassword);

        return Created(location, response);
    }
}
