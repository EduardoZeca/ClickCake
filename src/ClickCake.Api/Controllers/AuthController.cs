using ClickCake.Api.Data;
using ClickCake.Api.Dtos;
using ClickCake.Api.Models;
using ClickCake.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ClickCake.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UsuarioRepositorio _repositorio;
    private readonly IPasswordHasher<Usuario> _passwordHasher;
    private readonly TokenServico _tokenServico;

    public AuthController(
        UsuarioRepositorio repositorio,
        IPasswordHasher<Usuario> passwordHasher,
        TokenServico tokenServico)
    {
        _repositorio = repositorio;
        _passwordHasher = passwordHasher;
        _tokenServico = tokenServico;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Login) ||
            string.IsNullOrWhiteSpace(request.Senha))
        {
            return BadRequest(new
            {
                mensagem = "Informe login e senha."
            });
        }

        var usuario = await _repositorio.BuscarPorLoginAsync(
            request.Login.Trim());

        if (usuario == null || !usuario.Ativo)
        {
            return Unauthorized(new
            {
                mensagem = "Login ou senha invalidos."
            });
        }

        var resultado = _passwordHasher.VerifyHashedPassword(
            usuario,
            usuario.SenhaHash,
            request.Senha);

        if (resultado == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new
            {
                mensagem = "Login ou senha invalidos."
            });
        }

        var (token, expiracao) = _tokenServico.Gerar(usuario);

        return Ok(new
        {
            token,
            expiraEmUtc = expiracao,
            usuario = new
            {
                usuario.Id,
                usuario.Nome,
                usuario.Login,
                usuario.Perfil
            }
        });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            id = User.FindFirst("id")?.Value,
            nome = User.FindFirst("nome")?.Value,
            perfil = User.FindFirst("perfil")?.Value
        });
    }

    [Authorize(Roles = "Administrador")]
    [HttpGet("admin")]
    public IActionResult Admin()
    {
        return Ok(new
        {
            mensagem = "Acesso autorizado ao administrador."
        });
    }
}