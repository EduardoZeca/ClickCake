using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ClickCake.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickCake.Web.Controllers;

public class ContaController(IHttpClientFactory fabrica) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login() => View(new LoginWebDto());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginWebDto entrada)
    {
        if (!ModelState.IsValid) return View(entrada);
        try
        {
            var cliente = fabrica.CreateClient("ClickCakeApi");
            using var resposta = await cliente.PostAsJsonAsync("api/auth/login", entrada);
            if (!resposta.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", "Login ou senha invalidos.");
                return View(entrada);
            }

            using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            var raiz = json.RootElement;
            var usuario = raiz.GetProperty("usuario");
            var perfil = usuario.GetProperty("perfil").GetString();
            if (perfil != "Administrador")
            {
                ModelState.AddModelError("", "A Web e exclusiva para administradores.");
                return View(entrada);
            }

            var token = raiz.GetProperty("token").GetString()!;
            var expira = raiz.GetProperty("expiraEmUtc").GetDateTimeOffset();
            var identidade = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.GetProperty("id").ToString()),
                new Claim(ClaimTypes.Name, usuario.GetProperty("nome").GetString() ?? ""),
                new Claim(ClaimTypes.Role, perfil)
            }, CookieAuthenticationDefaults.AuthenticationScheme);

            var propriedades = new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = expira
            };
            propriedades.StoreTokens(new[]
            {
                new AuthenticationToken { Name = "access_token", Value = token }
            });
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identidade), propriedades);
            return RedirectToAction("Index", "Admin");
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError("", "Nao foi possivel conectar a API.");
            return View(entrada);
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sair()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    public IActionResult Negado() => Content("Acesso nao autorizado.");
}
