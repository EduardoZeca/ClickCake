using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickCake.Web.Controllers;

[Authorize(Roles = "Administrador")]
public sealed class PedidosGeraisController(IHttpClientFactory fabrica) : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Listar(
        string? situacao, int? vendedorId, string? inicio, string? fim)
    {
        var filtros = new List<string>();
        if (!string.IsNullOrWhiteSpace(situacao))
            filtros.Add("situacao=" + Uri.EscapeDataString(situacao));
        if (vendedorId.HasValue)
            filtros.Add("vendedorId=" + vendedorId.Value);
        if (!string.IsNullOrWhiteSpace(inicio))
            filtros.Add("inicio=" + Uri.EscapeDataString(inicio));
        if (!string.IsNullOrWhiteSpace(fim))
            filtros.Add("fim=" + Uri.EscapeDataString(fim));
        var caminho = "api/admin/pedidosgerais" +
            (filtros.Count == 0 ? "" : "?" + string.Join("&", filtros));
        return await Encaminhar(HttpMethod.Get, caminho);
    }

    [HttpGet]
    public async Task<IActionResult> Detalhes(int id)
    {
        if (id <= 0) return BadRequest();
        return await Encaminhar(HttpMethod.Get, $"api/admin/pedidosgerais/{id}");
    }

    [HttpGet]
    public async Task<IActionResult> Vendas() =>
        await Encaminhar(HttpMethod.Get, "api/admin/vendas");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirmar(int id, [FromBody] JsonElement dados)
    {
        if (id <= 0 || dados.ValueKind != JsonValueKind.Object) return BadRequest();
        return await Encaminhar(HttpMethod.Post,
            $"api/admin/pedidosgerais/{id}/confirmar", dados.GetRawText());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id, [FromBody] JsonElement dados)
    {
        if (id <= 0 || dados.ValueKind != JsonValueKind.Object) return BadRequest();
        return await Encaminhar(HttpMethod.Post,
            $"api/admin/pedidosgerais/{id}/cancelar", dados.GetRawText());
    }

    private async Task<IActionResult> Encaminhar(
        HttpMethod metodo, string caminho, string? json = null)
    {
        var token = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        try
        {
            var cliente = fabrica.CreateClient("ClickCakeApi");
            using var req = new HttpRequestMessage(metodo, caminho);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (json is not null)
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resposta = await cliente.SendAsync(req);
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
