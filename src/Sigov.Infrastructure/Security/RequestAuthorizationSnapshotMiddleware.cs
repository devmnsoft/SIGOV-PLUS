using Microsoft.AspNetCore.Http;
using Sigov.Application.Authorization;

namespace Sigov.Infrastructure.Security;

public sealed class RequestAuthorizationSnapshotMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IRequestAuthorizationSnapshot snapshot)
    {
        await snapshot.GetAsync(context.RequestAborted).ConfigureAwait(false);
        await next(context).ConfigureAwait(false);
    }
}
