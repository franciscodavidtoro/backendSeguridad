using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SistemaInventario.Api.Infrastructure.Security;

public class PassiveAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public PassiveAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // If JwtValidationMiddleware already populated HttpContext.User and it's authenticated, return success.
        var user = Context.User;
        if (user?.Identity != null && user.Identity.IsAuthenticated)
        {
            var ticket = new AuthenticationTicket(user, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        // Otherwise, no result - let Authorization handle challenge/forbid via ChallengeAsync/ForbidAsync
        return Task.FromResult(AuthenticateResult.NoResult());
    }
}
