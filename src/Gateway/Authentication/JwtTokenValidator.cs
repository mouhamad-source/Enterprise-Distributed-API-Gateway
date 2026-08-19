using System.IdentityModel.Tokens.Jwt; 
using System.Security.Claims; 
using System.Text; 
using Microsoft.Extensions.Options; 
using Microsoft.IdentityModel.Tokens; 


namespace Gateway.Authentication; 

public class JwtTokenValidator
{
    private readonly JwtSettings _settings ; 
    private readonly ILogger<JwtTokenValidator> _logger ; 


    public JwtTokenValidator(IOptions<JwtSettings> setting , ILogger<JwtTokenValidator> logger)
    {
        _settings = setting.Value ; 
        _logger = logger; 
    }


    public UserContext ValidateToken(string token)
    {
        if(string.IsNullOrEmpty(token))
        {
            throw new SecurityTokenException("Token is null or empty."); 
        }

        var tokenHandler = new JwtSecurityTokenHandler(); 
        var key = Encoding.UTF8.GetBytes(_settings.Secret);

        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };


            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                         ?? principal.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                throw new SecurityTokenException("User ID claim not found.");

            var role = principal.FindFirst(ClaimTypes.Role)?.Value ?? "User";
            var plan = principal.FindFirst("plan")?.Value ?? "Free";

            return new UserContext
            {
                UserId = userId,
                Role = role,
                Plan = plan,
                Claims = principal.Claims.ToDictionary(c => c.Type, c => (object)c.Value)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT validation failed.");
            throw; // سيتم التقاطها في Middleware وإرجاع 401
        }
    }
}