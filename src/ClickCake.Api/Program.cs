using System.Text;
using ClickCake.Api.Data;
using ClickCake.Api.Models;
using ClickCake.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Banco e servicos
builder.Services.AddScoped<UsuarioRepositorio>();
builder.Services.AddScoped<AdminInicializador>();
builder.Services.AddScoped<Banco>();

// Senhas
builder.Services.AddSingleton<
    IPasswordHasher<Usuario>,
    PasswordHasher<Usuario>>();

// Tokens
builder.Services.AddSingleton<TokenServico>();

var chave = builder.Configuration["Jwt:Chave"]
    ?? throw new InvalidOperationException(
        "Chave JWT nao configurada.");

if (Encoding.UTF8.GetByteCount(chave) < 32)
{
    throw new InvalidOperationException(
        "A chave JWT deve possuir pelo menos 32 bytes.");
}

var emissor = builder.Configuration["Jwt:Emissor"]
    ?? throw new InvalidOperationException(
        "Emissor JWT nao configurado.");

var publico = builder.Configuration["Jwt:Publico"]
    ?? throw new InvalidOperationException(
        "Publico JWT nao configurado.");

// Autenticacao JWT
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = emissor,

                ValidateAudience = true,
                ValidAudience = publico,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(chave)),

                NameClaimType = "nome",
                RoleClaimType = "perfil"
            };

        // Confere se a conta continua ativa.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var idTexto =
                    context.Principal?.FindFirst("id")?.Value;

                var perfil =
                    context.Principal?.FindFirst("perfil")?.Value;

                if (!int.TryParse(idTexto, out var id))
                {
                    context.Fail("Identidade invalida.");
                    return;
                }

                var repositorio =
                    context.HttpContext.RequestServices
                        .GetRequiredService<UsuarioRepositorio>();

                var usuario =
                    await repositorio.BuscarPorIdAsync(id);

                if (usuario == null ||
                    !usuario.Ativo ||
                    usuario.Perfil != perfil)
                {
                    context.Fail("Usuario sem autorizacao.");
                }
            }
        };
    });

// Autorizacao
builder.Services.AddAuthorization();

var app = builder.Build();

// Administrador inicial: somente em desenvolvimento.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();

    var inicializador = scope.ServiceProvider
        .GetRequiredService<AdminInicializador>();

    await inicializador.ExecutarAsync();
}

// Porta HTTP usada somente no computador local, via cabo USB (adb reverse).
// As demais requisicoes, inclusive as da Web, continuam usando HTTPS.
if (app.Environment.IsDevelopment())
{
    app.UseWhen(
        contexto => contexto.Connection.LocalPort != 5101,
        ramo => ramo.UseHttpsRedirection());
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
