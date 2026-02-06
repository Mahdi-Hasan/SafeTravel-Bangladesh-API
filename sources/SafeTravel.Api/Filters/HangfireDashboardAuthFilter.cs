using Hangfire.Dashboard;
using System.Net.Http.Headers;
using System.Text;

namespace SafeTravel.Api.Filters;

/// <summary>
/// Implements HTTP Basic Authentication for Hangfire Dashboard.
/// Reads credentials from configuration.
/// </summary>
public sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    private readonly string _username;
    private readonly string _password;

    public HangfireDashboardAuthFilter(string username, string password)
    {
        _username = username ?? throw new ArgumentNullException(nameof(username));
        _password = password ?? throw new ArgumentNullException(nameof(password));
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var authHeader = httpContext.Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            SetUnauthorizedResponse(httpContext);
            return false;
        }

        try
        {
            var encodedCredentials = authHeader["Basic ".Length..].Trim();
            var credentialBytes = Convert.FromBase64String(encodedCredentials);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);

            if (credentials.Length == 2 &&
                string.Equals(credentials[0], _username, StringComparison.Ordinal) &&
                string.Equals(credentials[1], _password, StringComparison.Ordinal))
            {
                return true;
            }
        }
        catch
        {
            // Invalid Base64 or parsing error
        }

        SetUnauthorizedResponse(httpContext);
        return false;
    }

    private static void SetUnauthorizedResponse(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = 401;
        httpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"Hangfire Dashboard\"";
    }
}
