using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClickCake.Mobile.Models;

namespace ClickCake.Mobile.Services;

public sealed partial class ApiCliente
{
    // Teste Android fisico por USB: adb reverse tcp:5101 tcp:5101.
    // Nao e um endereco de producao nem deve ser exposto pela internet.
    private const string EnderecoApi = "http://127.0.0.1:5101/";

    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(EnderecoApi),
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<LoginResposta> EntrarAsync(string login, string senha)
    {
        using var resposta = await _http.PostAsJsonAsync("api/auth/login",
            new { login, senha });
        if (resposta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            throw new InvalidOperationException("Login ou senha invalidos.");
        resposta.EnsureSuccessStatusCode();
        return await resposta.Content.ReadFromJsonAsync<LoginResposta>(Json)
            ?? throw new InvalidOperationException("Resposta de login vazia.");
    }

    public async Task<SincronizacaoResposta> SincronizarAsync(string token)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, "api/mobile/sincronizacao");
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await _http.SendAsync(requisicao);
        if (resposta.StatusCode == HttpStatusCode.Unauthorized)
            throw new SessaoExpiradaException();
        if (resposta.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Conta sem permissao de vendedor.");
        resposta.EnsureSuccessStatusCode();
        return await resposta.Content.ReadFromJsonAsync<SincronizacaoResposta>(Json)
            ?? throw new InvalidOperationException("Resposta de sincronizacao vazia.");
    }

    public async Task<ResultadoEnvio?> ConsultarEnvioAsync(string token, Guid identificador)
    {
        using var requisicao = Autenticada(HttpMethod.Get,
            $"api/mobile/pedidosgerais/{identificador}", token);
        using var resposta = await _http.SendAsync(requisicao);
        if (resposta.StatusCode == HttpStatusCode.NotFound) return null;
        if (resposta.StatusCode == HttpStatusCode.Unauthorized) throw new SessaoExpiradaException();
        resposta.EnsureSuccessStatusCode();
        return await resposta.Content.ReadFromJsonAsync<ResultadoEnvio>(Json)
            ?? throw new InvalidOperationException("Resposta de consulta vazia.");
    }

    public async Task<ResultadoEnvio> EnviarPedidoGeralAsync(string token, PedidoGeralEnvio envio)
    {
        using var requisicao = Autenticada(HttpMethod.Post, "api/mobile/pedidosgerais", token);
        requisicao.Content = JsonContent.Create(envio, options: Json);
        using var resposta = await _http.SendAsync(requisicao);
        if (resposta.StatusCode == HttpStatusCode.Unauthorized) throw new SessaoExpiradaException();
        if (resposta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            var erro = await resposta.Content.ReadFromJsonAsync<ErroEnvio>(Json);
            throw new RejeicaoEnvioException(erro?.Mensagem ?? "Lote rejeitado pela API.");
        }
        resposta.EnsureSuccessStatusCode();
        return await resposta.Content.ReadFromJsonAsync<ResultadoEnvio>(Json)
            ?? throw new InvalidOperationException("Resposta de envio vazia.");
    }

    private static HttpRequestMessage Autenticada(HttpMethod metodo, string url, string token)
    {
        var requisicao = new HttpRequestMessage(metodo, url);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return requisicao;
    }

    private sealed class ErroEnvio
    {
        public string Mensagem { get; set; } = "";
    }
}

public class SessaoExpiradaException : Exception
{
    public SessaoExpiradaException() : base("Sessao expirada. Entre novamente.") { }
}

public sealed class RejeicaoEnvioException(string mensagem) : Exception(mensagem);
