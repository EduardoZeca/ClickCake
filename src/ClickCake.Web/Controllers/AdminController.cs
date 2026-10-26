using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickCake.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class AdminController(IHttpClientFactory fabrica) : Controller
{
    private static readonly HashSet<string> Recursos = new(StringComparer.OrdinalIgnoreCase)
    {
        "estados", "cidades", "vendedores", "clientes",
        "categorias", "produtos", "formas", "condicoes"
    };

    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Dados(string tipo, string? validade, int dias = 7, int? vendedorId = null)
    {
        if (!Recursos.Contains(tipo)) return BadRequest();
        var caminho = $"api/admin/{tipo}";
        var filtros = new List<string>();
        if (tipo == "produtos" && !string.IsNullOrWhiteSpace(validade))
            filtros.Add($"validade={Uri.EscapeDataString(validade)}");
        if (tipo == "produtos") filtros.Add($"dias={dias}");
        if (tipo == "clientes" && vendedorId.HasValue)
            filtros.Add($"vendedorId={vendedorId.Value}");
        if (filtros.Count > 0) caminho += "?" + string.Join("&", filtros);
        return await Encaminhar(HttpMethod.Get, caminho);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salvar(string tipo, int? id, [FromBody] JsonElement dados)
    {
        if (!Recursos.Contains(tipo) || tipo == "condicoes") return BadRequest();
        var caminho = $"api/admin/{tipo}" + (id.HasValue ? $"/{id.Value}" : "");
        return await Encaminhar(id.HasValue ? HttpMethod.Put : HttpMethod.Post,
            caminho, dados.GetRawText());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Excluir(string tipo, int id)
    {
        if (!Recursos.Contains(tipo) || tipo == "condicoes") return BadRequest();
        return await Encaminhar(HttpMethod.Delete, $"api/admin/{tipo}/{id}");
    }

    [HttpGet]
    public async Task<IActionResult> Rota(int vendedorId)
    {
        if (vendedorId <= 0) return BadRequest();
        return await Encaminhar(HttpMethod.Get, $"api/admin/rotas/{vendedorId}");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProgramarRota(int vendedorId, [FromBody] JsonElement dados)
    {
        if (vendedorId <= 0) return BadRequest();
        return await Encaminhar(HttpMethod.Post, $"api/admin/rotas/{vendedorId}", dados.GetRawText());
    }

    private async Task<IActionResult> Encaminhar(HttpMethod metodo, string caminho, string? json = null)
    {
        var token = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        try
        {
            var cliente = fabrica.CreateClient("ClickCakeApi");
            using var requisicao = new HttpRequestMessage(metodo, caminho);
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (json != null)
                requisicao.Content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resposta = await cliente.SendAsync(requisicao);
            var corpo = await resposta.Content.ReadAsStringAsync();
            return new ContentResult
            {
                StatusCode = (int)resposta.StatusCode,
                Content = corpo,
                ContentType = "application/json; charset=utf-8"
            };
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { mensagem = "API indisponivel." });
        }
    }
}
