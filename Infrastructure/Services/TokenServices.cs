using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Model;

namespace Infrastructure.Services
{
    public interface ITokenService
    {
        public string GetToken(AuthenticateModel model);
    }

    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration; 
        
        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration; 
        }

        public string GetToken(AuthenticateModel model)
        {
            // authentication successful so generate jwt token
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Secret"]);

            if (!Int32.TryParse(_configuration["JwtTokenExpires"], out var expires))
            {
                expires = 60;
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, model.Username),
                    new Claim(ClaimTypes.UserData, model.GetServerAndPubId())
                }),
                Expires = DateTime.UtcNow.AddMinutes(expires),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}