using MediatR;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Adapter.Api.Model;
using UrlShortener.Application.Abstract.Primary.Queries;

namespace UrlShortener.Adapter.Api.Controllers;

[ApiController]
[Route("{alias}")]
public sealed class RedirectController : ControllerBase
{
    private readonly IMediator _mediator;

    public RedirectController(IMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(RequiresUnlockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveAlias(
        string alias,
        [FromQuery] string? token,
        CancellationToken cancellationToken)
    {
        GetLinkByAliasResult result = await _mediator.Send(
            new GetLinkByAliasQuery(alias, token),
            cancellationToken);

        return result switch
        {
            GetLinkByAliasResult.Redirect redirect => Redirect(redirect.DestinationUrl),
            GetLinkByAliasResult.RequiresUnlock => Ok(new RequiresUnlockResponse(true, alias)),
            GetLinkByAliasResult.NotFound => NotFound(),
            _ => NotFound()
        };
    }
}
