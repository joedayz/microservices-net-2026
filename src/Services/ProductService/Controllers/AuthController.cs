using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ProductService.DTOs;


namespace ProductService.Controllers;

/// <summary>
/// Controller para autenticación (desarrollo/laboratorio).
/// En producción se usaría Azure AD, Keycloak u otro IdP.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    // Usuarios simulados para el laboratorio
    private static readonly Dictionary<string, (string Password, string Role)> Users = new()
    {
        ["admin"] = ("admin123", "Admin"),
        ["reader"] = ("reader123", "Reader"),
        ["user"] = ("user123", "User")
    };

    public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Genera un JWT token para el usuario autenticado.
    /// Usuarios disponibles: admin/admin123, reader/reader123, user/user123
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Login([FromBody] LoginDto loginDto)
    {
        // Validar credenciales simuladas
        if (!Users.TryGetValue(loginDto.Username.ToLower(), out var userData) ||
            userData.Password != loginDto.Password)
        {
            _logger.LogWarning("Login fallido para usuario: {Username}", loginDto.Username);
            return Unauthorized(new { Message = "Credenciales inválidas" });
        }

        var (_, role) = userData;
        var token = GenerateJwtToken(loginDto.Username, role);
        var expiration = DateTime.UtcNow.AddMinutes(
            _configuration.GetValue<int>("Jwt:ExpirationMinutes", 60));

        _logger.LogInformation("Login exitoso: {Username} con rol {Role}",
            loginDto.Username, role);

        return Ok(new TokenResponseDto
        {
            Token = token,
            Expiration = expiration,
            Username = loginDto.Username,
            Role = role
        });
    }

    /// <summary>
    /// Muestra los usuarios disponibles para pruebas.
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetTestUsers()
    {
        var users = Users.Select(u => new
        {
            Username = u.Key,
            Password = u.Value.Password,
            Role = u.Value.Role
        });

        return Ok(users);
    }

    private string GenerateJwtToken(string username, string role)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = jwtSettings["Key"]
                  ?? "S3cur3K3y_F0r_D3v3l0pm3nt_Purp0s3s_Only_2025!";
        var issuer = jwtSettings["Issuer"] ?? "microservices-net-2025";
        var audience = jwtSettings["Audience"] ?? "microservices-api";
        var expirationMinutes = _configuration.GetValue<int>(
            "Jwt:ExpirationMinutes", 60);

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(
            securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)  // ← Clave para [Authorize(Roles)]
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}