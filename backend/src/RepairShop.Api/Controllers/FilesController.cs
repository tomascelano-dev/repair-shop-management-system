using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Files;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/files")]
public sealed class FilesController : ControllerBase
{
    /// <summary>
    /// Downloads a stored file. Works with the bearer token (same shop) or with a short-lived signed URL
    /// (exp + sig) so images can be used in &lt;img&gt; tags.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(Guid id, [FromQuery] long? exp, [FromQuery] string? sig,
        [FromServices] FileService files, [FromServices] IFileUrlSigner signer, CancellationToken ct)
    {
        FileDownload download;
        if (exp is not null && sig is not null)
        {
            if (!signer.Validate(id, exp.Value, sig)) throw new ForbiddenException("El link del archivo venció. Recargá la página.");
            download = await files.OpenAnyShopAsync(id, ct);
            Response.Headers.CacheControl = "private, max-age=600";
        }
        else
        {
            if (User.Identity?.IsAuthenticated != true) throw new UnauthorizedException("Iniciá sesión para ver el archivo.");
            download = await files.OpenAsync(CurrentUser.GetShopId(User), id, ct);
            Response.Headers.CacheControl = "private, no-store";
        }

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        var inline = download.ContentType.StartsWith("image/", StringComparison.Ordinal) || download.ContentType == "application/pdf";
        return inline
            ? File(download.Content, download.ContentType)
            : File(download.Content, download.ContentType, download.FileName);
    }
}
