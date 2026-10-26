using ClickCake.Api.Data;
using ClickCake.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace ClickCake.Api.Services;

public class AdminInicializador
{
    private readonly UsuarioRepositorio _repositorio;
    private readonly IConfiguration _configuration;
    private readonly IPasswordHasher<Usuario> _passwordHasher;

    public AdminInicializador(
        UsuarioRepositorio repositorio,
        IConfiguration configuration,
        IPasswordHasher<Usuario> passwordHasher)
    {
        _repositorio = repositorio;
        _configuration = configuration;
        _passwordHasher = passwordHasher;
    }

    public async Task ExecutarAsync()
    {
        if (await _repositorio.ExisteAdministradorAsync())
            return;

        var nome = _configuration["AdminInicial:Nome"];
        var login = _configuration["AdminInicial:Login"];
        var senha = _configuration["AdminInicial:Senha"];

        if (string.IsNullOrWhiteSpace(nome) ||
            string.IsNullOrWhiteSpace(login) ||
            string.IsNullOrWhiteSpace(senha))
        {
            throw new InvalidOperationException(
                "Credenciais do administrador inicial nao configuradas.");
        }

        if (senha.Length < 12)
        {
            throw new InvalidOperationException(
                "A senha inicial deve possuir pelo menos 12 caracteres.");
        }

        var usuario = new Usuario
        {
            Nome = nome,
            Login = login,
            Perfil = "Administrador",
            Ativo = true
        };

        usuario.SenhaHash =
            _passwordHasher.HashPassword(usuario, senha);

        await _repositorio.InserirAdministradorAsync(usuario);

        Console.WriteLine(
            "Administrador inicial cadastrado no MySQL.");
    }
}