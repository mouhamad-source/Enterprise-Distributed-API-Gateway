using Microsoft.IdentityModel.Tokens; 

namespace Gateway.Authentication; 


public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next ; 
    private readonly ILogger<AuthenticationMiddleware> _logger ; 


    public AuthenticationMiddleware(RequestDelegate next, ILogger<AuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context , JwtTokenValidator validator)
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();


        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();

            try
            {
                var userContext = validator.ValidateToken(token);
                context.Items["UserContext"] = userContext;
                _logger.LogInformation("User {UserId} authenticated with plan {Plan}.", userContext.UserId, userContext.Plan);
            }
            catch (SecurityTokenException ex)
            {
                _logger.LogWarning(ex, "Invalid JWT token.");
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized: Invalid token.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during authentication.");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Internal Server Error during authentication.");
                return;
            }
        }else
        {
            _logger.LogDebug("No Bearer token found.");
        }

        await _next(context); 
    }
}