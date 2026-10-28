using ClickCake.Mobile.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ClickCake.Mobile;

public partial class MainPage
{
    private VerticalStackLayout? _listaHistoricoCliente;
    private Label? _mensagemHistoricoCliente;
    private int? _clienteHistoricoId;

    private void MontarHistoricoCliente()
    {
        var painel = new VerticalStackLayout { Spacing = 6 };
        painel.Children.Add(new Label
        {
            Text = "Historico do cliente", FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1E293B")
        });
        painel.Children.Add(Texto("Consultar pedidos anteriores e reutilizar itens com os precos atuais."));
        var consultar = Botao("Consultar historico / repetir itens", "#475569");
        consultar.Clicked += async (_, _) => await AtualizarHistoricoClienteAsync(_clienteHistoricoId);
        painel.Children.Add(consultar);
        _mensagemHistoricoCliente = Texto("");
        painel.Children.Add(_mensagemHistoricoCliente);
        _listaHistoricoCliente = new VerticalStackLayout { Spacing = 6 };
        painel.Children.Add(_listaHistoricoCliente);
        AreaAtendimento.Children.Insert(1, new Border
        {
            Padding = new Thickness(8), BackgroundColor = Color.FromArgb("#F8FAFC"),
            Stroke = new SolidColorBrush(Color.FromArgb("#94A3B8")),
            StrokeThickness = 1, Content = painel
        });
    }

    private async Task AtualizarHistoricoClienteAsync(int? clienteId)
    {
        _clienteHistoricoId = clienteId;
        if (_listaHistoricoCliente == null || _mensagemHistoricoCliente == null) return;
        _listaHistoricoCliente.Children.Clear();
        if (_dadosLocais == null || !clienteId.HasValue)
        {
            _mensagemHistoricoCliente.Text = "Nenhum cliente em atendimento.";
            return;
        }

        var dados = await _dadosLocais.LerResultadosAsync();
        if (dados == null)
        {
            _mensagemHistoricoCliente.Text = "Sem historico sincronizado. Atualize os resultados da loja.";
            return;
        }

        var pedidos = dados.Pedidos.Where(p => p.ClienteId == clienteId.Value)
            .OrderByDescending(p => p.PedidoGeralId)
            .ThenByDescending(p => p.Id)
            .Take(5).ToList();
        _mensagemHistoricoCliente.Text = pedidos.Count == 0
            ? "Nao existem pedidos anteriores deste cliente na ultima sincronizacao."
            : $"{pedidos.Count} ultimo(s) pedido(s) encontrado(s).";

        foreach (var pedido in pedidos)
        {
            var itens = dados.Itens.Where(i => i.PedidoId == pedido.Id).ToList();
            var bloco = new VerticalStackLayout
            {
                Spacing = 4, Padding = new Thickness(8),
                BackgroundColor = Colors.White
            };
            bloco.Children.Add(Texto(
                $"Pedido #{pedido.Id} - {pedido.Situacao} | Previsto R$ {pedido.TotalPrevisto:N2}"));
            foreach (var item in itens)
                bloco.Children.Add(Texto($"{item.Produto} x{item.QuantidadeSolicitada}"));

            if (itens.Count > 0)
            {
                var clienteDoHistorico = clienteId.Value;
                var itensParaCopiar = itens;
                var repetir = Botao("Repetir itens deste pedido", "#047857");
                repetir.Clicked += async (_, _) => await ExecutarPedidoUiAsync(() =>
                    RepetirItensHistoricoAsync(clienteDoHistorico, itensParaCopiar));
                bloco.Children.Add(repetir);
            }
            _listaHistoricoCliente.Children.Add(bloco);
        }
    }

    private async Task RepetirItensHistoricoAsync(int clienteId, List<ResultadoItem> itens)
    {
        if (_dadosLocais == null || string.IsNullOrEmpty(_cicloAtualId))
            throw new InvalidOperationException("Inicie ou retome um ciclo antes de repetir itens.");
        var ciclo = await _dadosLocais.BuscarCicloAsync(_cicloAtualId);
        if (ciclo?.Situacao != "EmAndamento")
            throw new InvalidOperationException("Somente um ciclo em andamento permite repetir itens.");

        var visitas = await _dadosLocais.ListarVisitasAsync(_cicloAtualId);
        var visitaAtual = _visitaEdicaoId.HasValue
            ? visitas.FirstOrDefault(v => v.Id == _visitaEdicaoId.Value)
            : visitas.FirstOrDefault(v => v.Situacao == "Pendente");
        if (visitaAtual?.ClienteId != clienteId)
            throw new InvalidOperationException("Somente o cliente da visita atual pode receber estes itens.");
        if (_carrinho.Count > 0)
            throw new InvalidOperationException("Retire os itens do carrinho antes de repetir outro pedido.");

        var novos = new List<ItemCarrinho>();
        var indisponiveis = new List<string>();
        foreach (var item in itens)
        {
            var produto = _catalogoProdutos.FirstOrDefault(p => p.Id == item.ProdutoId &&
                p.DataValidade.Date >= DateTime.Today);
            if (produto == null)
            {
                indisponiveis.Add(item.Produto);
                continue;
            }
            novos.Add(new ItemCarrinho
            {
                ProdutoId = produto.Id,
                NomeProduto = produto.Nome,
                PrecoUnitario = Math.Round(produto.Preco, 2, MidpointRounding.AwayFromZero),
                Quantidade = item.QuantidadeSolicitada
            });
        }
        if (novos.Count == 0)
            throw new InvalidOperationException("Nenhum produto deste pedido permanece disponivel no catalogo.");

        _carrinho.AddRange(novos);
        RenderizarCarrinho();
        MensagemCiclos.Text = $"{novos.Count} produto(s) copiado(s) para o carrinho com os precos atuais." +
            (indisponiveis.Count == 0 ? "" :
                $" Nao copiados (inativos/vencidos): {string.Join(", ", indisponiveis)}.") +
            " Escolha a condicao e as formas de pagamento antes de salvar.";
    }

    private void LimparHistoricoCliente()
    {
        _clienteHistoricoId = null;
        _listaHistoricoCliente?.Children.Clear();
        if (_mensagemHistoricoCliente != null) _mensagemHistoricoCliente.Text = "";
    }
}
