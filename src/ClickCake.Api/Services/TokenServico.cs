using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClickCake.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace ClickCake.Api.Services;

public class TokenServico
{
    private readonly IConfiguration _configuration;

    public TokenServico(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiraEmUtc) Gerar(Usuario usuario)
    {
        var chave = _configuration["Jwt:Chave"]
            ?? throw new InvalidOperationException(
                "Chave JWT nao configurada.");

        var emissor = _configuration["Jwt:Emissor"]
            ?? throw new InvalidOperationException(
                "Emissor JWT nao configurado.");

        var publico = _configuration["Jwt:Publico"]
            ?? throw new InvalidOperationException(
                "Publico JWT nao configurado.");

        var chaveSimetrica = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(chave));

        var credenciais = new SigningCredentials(
            chaveSimetrica,
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("id", usuario.Id.ToString()),
            new("nome", usuario.Nome),
            new("perfil", usuario.Perfil),
            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString())
        };

        var expiracao = DateTime.UtcNow.AddHours(2);

        var token = new JwtSecurityToken(
            issuer: emissor,
            audience: publico,
            claims: claims,
            expires: expiracao,
            signingCredentials: credenciais
        );

        return (
            new JwtSecurityTokenHandler().WriteToken(token),
            expiracao
        );
    }
}