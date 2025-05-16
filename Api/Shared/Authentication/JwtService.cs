using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api.Domain.Dtos;
using Api.Shared.Dtos;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Api.Shared.Authentication;

public class JwtService
{
    private readonly JwtSettings _jwtSettings;

    public JwtService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    /// <summary>
    /// Encode a UserSession object into a JWT token
    /// </summary>
    public string EncodeToken(UserSession session)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

        var claims = new[]
        {
            new Claim("sessionName", session.SessionName.ToString()),
            new Claim("expiryTime", session.ExpiryTime.ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            // The actual token expiry in JWT should be slightly longer than our session expiry
            // This gives us time to handle expiry on our own terms
            Expires = DateTimeOffset.FromUnixTimeSeconds(session.ExpiryTime + 300).UtcDateTime, // 5 min buffer
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha512Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Decode and validate a JWT token, returning the UserSession
    /// </summary>
    public (UserSession? Session, bool IsValid) DecodeAndValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return (null, false);

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false, // We handle expiry ourselves based on the claim
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;

            if (!Guid.TryParse(jwtToken.Claims.First(x => x.Type == "sessionName").Value, out Guid sessionName))
                return (null, false);

            if (!long.TryParse(jwtToken.Claims.First(x => x.Type == "expiryTime").Value, out long expiryTime))
                return (null, false);

            var session = new UserSession
            {
                SessionName = sessionName,
                ExpiryTime = expiryTime
            };

            return (session, true);
        }
        catch
        {
            return (null, false);
        }
    }

    /// <summary>
    /// Simple decode without validation (just to get session info)
    /// </summary>
    public UserSession? DecodeToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            if (!tokenHandler.CanReadToken(token))
                return null;

            var jwtToken = tokenHandler.ReadJwtToken(token);

            if (!Guid.TryParse(jwtToken.Claims.First(x => x.Type == "sessionName").Value, out Guid sessionName))
                return null;

            if (!long.TryParse(jwtToken.Claims.First(x => x.Type == "expiryTime").Value, out long expiryTime))
                return null;

            return new UserSession
            {
                SessionName = sessionName,
                ExpiryTime = expiryTime
            };
        }
        catch
        {
            return null;
        }
    }
}