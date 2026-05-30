using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace APIGateWay.BusinessLayer.Helpers.token
{
    public class TokenGeneration
    {
        private readonly IConfiguration _configuration;
        public TokenGeneration(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public string GenerateJwtToken(
             Guid userId,
             string userName,
             int? role,
             string dbName,
             string? Team,
             string? PreviewUrl,

             Guid sessionId,
             Guid jwtId,
             DateTime tokenIssuedAt,
             DateTime tokenExpiresAt
         )
        {
            var securityKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"])
            );

            var credentials = new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256
            );

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, userName),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim("Team", Team.ToString()),
                new Claim("DbName", dbName ?? ""),
                new Claim("PreviewUrl", PreviewUrl ?? ""),
                new Claim("SessionId", sessionId.ToString()),
                new Claim("JwtId", jwtId.ToString()),
                new Claim("TokenIssuedAt", tokenIssuedAt.ToString("O")),
                new Claim("TokenExpiresAt", tokenExpiresAt.ToString("O")),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: "WG",
                audience: "WGNest",
                claims: claims,
                expires: tokenExpiresAt,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
