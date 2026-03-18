using MediatR;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Adapter.Api.Model;
using UrlShortener.Application.Abstract.Primary.Commands;

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

    [HttpPost("{alias}/unlock")]
    [ProducesResponseType(typeof(UnlockLinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlockLink(
        string alias,
        [FromBody] UnlockLinkRequest request,
        CancellationToken cancellationToken)
    {
        UnlockLinkResult result = await _mediator.Send(
            new UnlockLinkCommand(alias, request.Password),
            cancellationToken);

        return result switch
        {
            UnlockLinkResult.Success success => Ok(new UnlockLinkResponse(success.Token)),
            UnlockLinkResult.NotFound => NotFound(),
            UnlockLinkResult.InvalidPassword => BadRequest(new { message = "Invalid password." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
