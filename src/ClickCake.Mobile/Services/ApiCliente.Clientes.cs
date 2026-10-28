using System.Net;
using System.Net.Http.Json;
using ClickCake.Mobile.Models;

namespace ClickCake.Mobile.Services;

public sealed partial class ApiCliente
{
    public async Task<List<CidadeCadastroDto>> ListarCidadesAsync(string token)
    {
        using var requisicao = Autenticada(HttpMethod.Get, "api/mobile/clientes/cidades", token);
        using var resposta = await _http.SendAsync(requisicao);
        if (resposta.StatusCode == HttpStatusCode.Unauthorized) throw new SessaoExpiradaException();
        resposta.EnsureSuccessStatusCode();
        return await resposta.Content.ReadFromJsonAsync<List<CidadeCadastroDto>>(Json)
            ?? throw new InvalidOperationException("API nao retornou as cidades.");
    }

    public async Task<NovoClienteResposta> CadastrarClienteAsync(string token, NovoClienteDto cliente)
    {
        using var requisicao = Autenticada(HttpMethod.Post, "api/mobile/clientes", token);
        requisicao.Content = JsonContent.Create(cliente, options: Json);
        using var resposta = await _http.SendAsync(requisicao);
        if (resposta.StatusCode == HttpStatusCode.Unauthorized) throw new SessaoExpiradaException();
        if (!resposta.IsSuccessStatusCode)
        {
            var erro = await resposta.Content.ReadFromJsonAsync<ErroCliente>(Json);
            throw new InvalidOperationException(erro?.Mensagem ?? "Nao foi possivel cadastrar o cliente.");
        }
        return await resposta.Content.ReadFromJsonAsync<NovoClienteResposta>(Json)
            ?? throw new InvalidOperationException("Resposta vazia do cadastro.");
    }

    private sealed class ErroCliente
    {
        public string Mensagem { get; set; } = "";
    }
}
