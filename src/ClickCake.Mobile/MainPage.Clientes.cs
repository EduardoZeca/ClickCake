using ClickCake.Mobile.Models;
using ClickCake.Mobile.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ClickCake.Mobile;

public partial class MainPage
{
    private readonly Dictionary<string, Entry> _camposNovoCliente = new();
    private Picker? _cidadeNovoCliente;
    private Label? _mensagemNovoCliente;
    private VerticalStackLayout? _formNovoCliente;
    private Button? _enviarNovoCliente;

    private void MontarCadastroClientes()
    {
        var painel = new VerticalStackLayout { Spacing = 8 };
        painel.Children.Add(new Label
        {
            Text = "Cadastro de cliente (online)", FontAttributes = FontAttributes.Bold,
            FontSize = 19, TextColor = Color.FromArgb("#1E293B")
        });
        painel.Children.Add(Texto("O cliente cadastrado so entra na rota apos programacao do administrador."));

        var alternar = Botao("Abrir / fechar cadastro", "#475569");
        alternar.Clicked += (_, _) =>
        {
            if (_formNovoCliente != null) _formNovoCliente.IsVisible = !_formNovoCliente.IsVisible;
        };
        painel.Children.Add(alternar);

        _formNovoCliente = new VerticalStackLayout { Spacing = 8, IsVisible = false };
        _formNovoCliente.Children.Add(Campo("Nome", "Nome do cliente *"));
        _cidadeNovoCliente = new Picker
        {
            Title = "Selecione a cidade *", TextColor = Colors.Black,
            ItemDisplayBinding = new Binding(nameof(CidadeCadastroDto.Descricao))
        };
        EstilizarSeletor(_cidadeNovoCliente);
        _formNovoCliente.Children.Add(_cidadeNovoCliente);
        var atualizarCidades = Botao("Carregar cidades da API", "#1D4ED8");
        atualizarCidades.Clicked += async (_, _) => await CarregarCidadesCadastroAsync();
        _formNovoCliente.Children.Add(atualizarCidades);

        foreach (var (chave, descricao) in new[]
        {
            ("Documento", "Documento"), ("Telefone", "Telefone"),
            ("Email", "E-mail"), ("Logradouro", "Logradouro"),
            ("Numero", "Numero"), ("Complemento", "Complemento"),
            ("Bairro", "Bairro"), ("Cep", "CEP")
        })
            _formNovoCliente.Children.Add(Campo(chave, descricao));

        _enviarNovoCliente = Botao("Cadastrar cliente na API", "#047857");
        _enviarNovoCliente.Clicked += AoCadastrarNovoCliente;
        _formNovoCliente.Children.Add(_enviarNovoCliente);
        painel.Children.Add(_formNovoCliente);
        _mensagemNovoCliente = Texto("");
        painel.Children.Add(_mensagemNovoCliente);

        var borda = new Border
        {
            Padding = new Thickness(12), BackgroundColor = Colors.White,
            Stroke = new SolidColorBrush(Color.FromArgb("#CBD5E1")),
            StrokeThickness = 1, Content = painel
        };
        AreaDados.Children.Insert(4, borda);
    }

    private Entry Campo(string chave, string descricao)
    {
        var campo = new Entry
        {
            Placeholder = descricao,
            TextColor = Colors.Black,
            BackgroundColor = Color.FromArgb("#EFF6FF"),
            HeightRequest = 48
        };
        if (chave == "Email") campo.Keyboard = Keyboard.Email;
        if (chave is "Telefone" or "Cep") campo.Keyboard = Keyboard.Telephone;
        _camposNovoCliente[chave] = campo;
        return campo;
    }

    private string? LerCampo(string chave)
    {
        var texto = _camposNovoCliente[chave].Text?.Trim();
        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }

    private async Task CarregarCidadesCadastroAsync()
    {
        if (_cidadeNovoCliente == null || string.IsNullOrEmpty(_token)) return;
        try
        {
            var lista = await _api.ListarCidadesAsync(_token);
            _cidadeNovoCliente.ItemsSource = lista;
            if (_mensagemNovoCliente != null)
                _mensagemNovoCliente.Text = $"{lista.Count} cidade(s) carregada(s).";
        }
        catch (SessaoExpiradaException)
        {
            await EncerrarSessaoAsync();
            MensagemLogin.Text = "Sessao expirada. Entre novamente.";
        }
        catch (Exception ex)
        {
            if (_mensagemNovoCliente != null)
                _mensagemNovoCliente.Text = $"Nao foi possivel carregar cidades: {ex.Message}";
        }
    }

    private async void AoCadastrarNovoCliente(object? sender, EventArgs e)
    {
        if (_enviarNovoCliente == null || _cidadeNovoCliente == null) return;
        if (string.IsNullOrEmpty(_token)) return;
        if (LerCampo("Nome") is not { } nome ||
            _cidadeNovoCliente.SelectedItem is not CidadeCadastroDto cidade)
        {
            if (_mensagemNovoCliente != null)
                _mensagemNovoCliente.Text = "Informe nome e selecione a cidade.";
            return;
        }

        _enviarNovoCliente.IsEnabled = false;
        try
        {
            var novo = new NovoClienteDto
            {
                Nome = nome, CidadeId = cidade.Id, Documento = LerCampo("Documento"),
                Telefone = LerCampo("Telefone"), Email = LerCampo("Email"),
                Logradouro = LerCampo("Logradouro"), Numero = LerCampo("Numero"),
                Complemento = LerCampo("Complemento"), Bairro = LerCampo("Bairro"),
                Cep = LerCampo("Cep")
            };
            var resposta = await _api.CadastrarClienteAsync(_token, novo);
            if (_mensagemNovoCliente != null)
                _mensagemNovoCliente.Text =
                    $"Cliente #{resposta.Id} cadastrado. Codigo provisório {resposta.CodigoVisita}. " +
                    "Aguarde a programacao da rota pelo administrador.";
            foreach (var campo in _camposNovoCliente.Values) campo.Text = "";
            _cidadeNovoCliente.SelectedIndex = -1;
            if (_formNovoCliente != null) _formNovoCliente.IsVisible = false;
        }
        catch (SessaoExpiradaException)
        {
            await EncerrarSessaoAsync();
            MensagemLogin.Text = "Sessao expirada. Entre novamente.";
        }
        catch (Exception ex)
        {
            if (_mensagemNovoCliente != null)
                _mensagemNovoCliente.Text = $"Cadastro nao enviado: {ex.Message}. Dados mantidos.";
        }
        finally
        {
            _enviarNovoCliente.IsEnabled = true;
        }
    }

    private void LimparCadastroClientes()
    {
        _cidadeNovoCliente?.ClearValue(Picker.ItemsSourceProperty);
        if (_mensagemNovoCliente != null) _mensagemNovoCliente.Text = "";
        if (_formNovoCliente != null) _formNovoCliente.IsVisible = false;
        foreach (var campo in _camposNovoCliente.Values) campo.Text = "";
    }
}
