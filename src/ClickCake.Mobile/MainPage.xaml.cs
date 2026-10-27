using System.Collections.ObjectModel;
using ClickCake.Mobile.Data;
using ClickCake.Mobile.Models;
using ClickCake.Mobile.Services;
using Microsoft.Maui.Storage;

namespace ClickCake.Mobile;

public partial class MainPage : ContentPage
{
    private const string ChaveToken = "ClickCakeToken";
    private const string ChaveVendedor = "ClickCakeVendedorId";
    private const string ChaveNome = "ClickCakeVendedorNome";

    private readonly ApiCliente _api = new();
    private DadosLocais? _dadosLocais;
    private string? _token;
    private string _nome = "";

    public ObservableCollection<ClienteExibicao> Clientes { get; } = new();
    public ObservableCollection<ProdutoExibicao> Produtos { get; } = new();
    public ObservableCollection<CondicaoExibicao> Condicoes { get; } = new();

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;
        Loaded += InicializarAoCarregar;
    }

    private async void InicializarAoCarregar(object? sender, EventArgs e)
    {
        Loaded -= InicializarAoCarregar;
        try
        {
            _token = await SecureStorage.Default.GetAsync(ChaveToken);
            var idTexto = await SecureStorage.Default.GetAsync(ChaveVendedor);
            _nome = await SecureStorage.Default.GetAsync(ChaveNome) ?? "Vendedor";
            if (!string.IsNullOrWhiteSpace(_token) && int.TryParse(idTexto, out var id))
            {
                await PrepararDadosAsync(id);
                await SincronizarDadosAsync();
            }
        }
        catch (Exception ex)
        {
            MensagemLogin.Text = $"Nao foi possivel recuperar a sessao: {ex.Message}";
        }
    }

    private async void AoEntrar(object? sender, EventArgs e)
    {
        var login = EntradaLogin.Text?.Trim() ?? "";
        var senha = EntradaSenha.Text ?? "";
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(senha))
        {
            MensagemLogin.Text = "Informe login e senha.";
            return;
        }

        BotaoEntrar.IsEnabled = false;
        MensagemLogin.Text = "Autenticando...";
        try
        {
            var resposta = await _api.EntrarAsync(login, senha);
            if (resposta.Usuario.Perfil != "Vendedor")
            {
                MensagemLogin.Text = "Somente vendedores acessam o aplicativo.";
                return;
            }

            _token = resposta.Token;
            _nome = resposta.Usuario.Nome;
            await SecureStorage.Default.SetAsync(ChaveToken, resposta.Token);
            await SecureStorage.Default.SetAsync(ChaveVendedor, resposta.Usuario.Id.ToString());
            await SecureStorage.Default.SetAsync(ChaveNome, _nome);
            EntradaSenha.Text = "";

            await PrepararDadosAsync(resposta.Usuario.Id);
            await SincronizarDadosAsync();
        }
        catch (Exception ex)
        {
            MensagemLogin.Text = $"Falha no login: {ex.Message}";
        }
        finally
        {
            BotaoEntrar.IsEnabled = true;
        }
    }

    private async Task PrepararDadosAsync(int vendedorId)
    {
        _dadosLocais = new DadosLocais(vendedorId);
        await _dadosLocais.InicializarAsync();
        AreaLogin.IsVisible = false;
        AreaDados.IsVisible = true;
        UsuarioLogado.Text = $"Vendedor: {_nome}";
        await ExibirCacheAsync();
    }

    private async Task ExibirCacheAsync()
    {
        if (_dadosLocais == null) return;
        Clientes.Clear();
        foreach (var cliente in await _dadosLocais.ListarRotaAsync()) Clientes.Add(cliente);
        Produtos.Clear();
        foreach (var produto in await _dadosLocais.ListarProdutosAsync()) Produtos.Add(produto);
        Condicoes.Clear();
        foreach (var condicao in await _dadosLocais.ListarCondicoesAsync()) Condicoes.Add(condicao);

        StatusClientes.Text = $"{Clientes.Count} cliente(s) na ordem programada";
        StatusProdutos.Text = $"{Produtos.Count} produto(s) disponivel(is) na ultima sincronizacao";
        StatusCondicoes.Text = $"{Condicoes.Count} condicao(oes) de pagamento";
        ResumoLocal.Text = await _dadosLocais.ObterResumoSincronizacaoAsync();
    }

    private async void AoSincronizar(object? sender, EventArgs e) => await SincronizarDadosAsync();

    private async Task SincronizarDadosAsync()
    {
        if (_dadosLocais == null || string.IsNullOrWhiteSpace(_token)) return;
        BotaoSincronizar.IsEnabled = false;
        MensagemSincronizacao.Text = "Sincronizando com a loja...";
        try
        {
            var resposta = await _api.SincronizarAsync(_token);
            await _dadosLocais.SalvarSincronizacaoAsync(resposta);
            await ExibirCacheAsync();
            MensagemSincronizacao.Text = "Sincronizacao concluida. Dados atualizados no SQLite.";
        }
        catch (SessaoExpiradaException)
        {
            await EncerrarSessaoAsync();
            MensagemLogin.Text = "Sessao expirada. Entre novamente.";
        }
        catch (Exception ex)
        {
            // Falha de rede nunca apaga o roteiro ou catalogo ja salvo.
            MensagemSincronizacao.Text = $"Sem sincronizacao: {ex.Message}. Dados locais preservados.";
        }
        finally
        {
            BotaoSincronizar.IsEnabled = true;
        }
    }

    private async void AoSair(object? sender, EventArgs e) => await EncerrarSessaoAsync();

    private Task EncerrarSessaoAsync()
    {
        SecureStorage.Default.Remove(ChaveToken);
        SecureStorage.Default.Remove(ChaveVendedor);
        SecureStorage.Default.Remove(ChaveNome);
        _token = null;
        _dadosLocais = null;
        _nome = "";
        Clientes.Clear();
        Produtos.Clear();
        Condicoes.Clear();
        AreaDados.IsVisible = false;
        AreaLogin.IsVisible = true;
        MensagemLogin.Text = "";
        EntradaSenha.Text = "";
        return Task.CompletedTask;
    }
}
