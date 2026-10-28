using System.Net;
using System.Net.Http.Json;
using ClickCake.Mobile.Models;

namespace ClickCake.Mobile.Services;

public sealed partial class ApiCliente
{
    public async Task<ResultadosResposta> ConsultarResultadosAsync(string token)
    {
        using var requisicao = Autenticada(HttpMethod.Get, "api/mobile/resultados", token);
        using var resposta = await _http.SendAsync(requisicao);
        if (resposta.StatusCode == HttpStatusCode.Unauthorized)
            throw new SessaoExpiradaException();
        resposta.EnsureSuccessStatusCode();
        return await resposta.Content.ReadFromJsonAsync<ResultadosResposta>(Json)
            ?? throw new InvalidOperationException("Resposta de resultados vazia.");
    }
}
