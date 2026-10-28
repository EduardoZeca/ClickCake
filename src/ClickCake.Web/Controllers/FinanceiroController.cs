using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickCake.Web.Controllers;

[Authorize(Roles = "Administrador")]
public sealed class FinanceiroController(IHttpClientFactory fabrica) : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Parcelas(int? clienteId, string? situacao, string? inicio, string? fim)
    {
        var filtros = new List<string>();
        if (clienteId.HasValue) filtros.Add("clienteId=" + clienteId.Value);
        if (!string.IsNullOrWhiteSpace(situacao))
            filtros.Add("situacao=" + Uri.EscapeDataString(situacao));
        if (!string.IsNullOrWhiteSpace(inicio))
            filtros.Add("inicio=" + Uri.EscapeDataString(inicio));
        if (!string.IsNullOrWhiteSpace(fim))
            filtros.Add("fim=" + Uri.EscapeDataString(fim));
        return await Encaminhar(HttpMethod.Get, "api/admin/financeiro/parcelas" +
            (filtros.Count == 0 ? "" : "?" + string.Join("&", filtros)));
    }

    [HttpGet]
    public async Task<IActionResult> Simular(int id, string data)
    {
        if (id <= 0 || string.IsNullOrWhiteSpace(data)) return BadRequest();
        return await Encaminhar(HttpMethod.Get,
            $"api/admin/financeiro/parcelas/{id}/simular?data={Uri.EscapeDataString(data)}");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Receber(int id, [FromBody] JsonElement dados)
    {
        if (id <= 0 || dados.ValueKind != JsonValueKind.Object) return BadRequest();
        return await Encaminhar(HttpMethod.Post,
            $"api/admin/financeiro/parcelas/{id}/receber", dados.GetRawText());
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
            return new ContentResult
            {
                StatusCode = (int)resposta.StatusCode,
                Content = await resposta.Content.ReadAsStringAsync(),
                ContentType = "application/json; charset=utf-8"
            };
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { mensagem = "API indisponivel." });
        }
    }
}
