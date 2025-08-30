using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace IntegrationGateway.Api.Controllers;

/// <summary>
/// 仅用于开发环境的简单认证控制器
/// </summary>
[ApiController]
[Route("api/dev/auth")]
public class DevAuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public DevAuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// 生成开发用JWT token
    /// </summary>
    [HttpGet("token")]
    public ActionResult<object> GetToken([FromQuery] string username = "testuser")
    {
        var secretKey = _configuration["Jwt:SecretKey"];
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        
        if (string.IsNullOrEmpty(secretKey))
        {
            return BadRequest("JWT SecretKey not configured");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("sub", username)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new
        {
            token = tokenString,
            expires = token.ValidTo,
            usage = "Copy the token value and paste it in Swagger Authorize dialog"
        });
    }
}