using ClickCake.Mobile.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ClickCake.Mobile;

public partial class MainPage
{
    private VerticalStackLayout? _listaResultados;
    private Label? _mensagemResultados;
    private Button? _botaoResultados;

    private void MontarPainelResultados()
    {
        var painel = new VerticalStackLayout { Spacing = 8 };
        painel.Children.Add(new Label
        {
            Text = "Resultados e historico de pedidos",
            FontSize = 19, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1E293B")
        });
        painel.Children.Add(Texto("Solicitado, atendido, cortado e parcelas definitivas, quando disponiveis."));
        _botaoResultados = Botao("Atualizar resultados da loja", "#1D4ED8");
        _botaoResultados.Clicked += AoAtualizarResultados;
        painel.Children.Add(_botaoResultados);
        _mensagemResultados = Texto("");
        painel.Children.Add(_mensagemResultados);
        _listaResultados = new VerticalStackLayout { Spacing = 10 };
        painel.Children.Add(_listaResultados);
        var borda = new Border
        {
            Padding = new Thickness(12),
            BackgroundColor = Colors.White,
            Stroke = new SolidColorBrush(Color.FromArgb("#CBD5E1")),
            StrokeThickness = 1,
            Content = painel
        };
        // Mantem os botoes Sincronizar/Sair e a mensagem no topo da tela.
        AreaDados.Children.Insert(4, borda);
    }

    private async Task ExibirResultadosCacheAsync()
    {
        if (_dadosLocais == null) return;
        var dados = await _dadosLocais.LerResultadosAsync();
        RenderizarResultados(dados);
    }

    private async Task TentarSincronizarResultadosAsync()
    {
        try
        {
            await SincronizarResultadosAsync();
        }
        catch (ClickCake.Mobile.Services.SessaoExpiradaException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (_mensagemResultados != null)
                _mensagemResultados.Text = "Resultados nao atualizados: " + ex.Message +
                    ". O cache local foi preservado.";
        }
    }

    private async Task SincronizarResultadosAsync()
    {
        if (_dadosLocais == null || string.IsNullOrWhiteSpace(_token)) return;
        var resultado = await _api.ConsultarResultadosAsync(_token);
        await _dadosLocais.SalvarResultadosAsync(resultado);
        RenderizarResultados(resultado);
        if (_mensagemResultados != null)
            _mensagemResultados.Text = "Resultados sincronizados e salvos no SQLite.";
    }

    private async void AoAtualizarResultados(object? sender, EventArgs e)
    {
        if (_botaoResultados == null || _dadosLocais == null) return;
        _botaoResultados.IsEnabled = false;
        try
        {
            await ExibirResultadosCacheAsync();
            await SincronizarResultadosAsync();
        }
        catch (ClickCake.Mobile.Services.SessaoExpiradaException)
        {
            await EncerrarSessaoAsync();
            MensagemLogin.Text = "Sessao expirada. Entre novamente para consultar os resultados.";
        }
        catch (Exception ex)
        {
            if (_mensagemResultados != null)
                _mensagemResultados.Text = "Sem conexao: " + ex.Message +
                    ". Resultados ja salvos continuam disponiveis.";
        }
        finally
        {
            _botaoResultados.IsEnabled = true;
        }
    }

    private void LimparPainelResultados()
    {
        _listaResultados?.Children.Clear();
        if (_mensagemResultados != null) _mensagemResultados.Text = "";
    }

    private void RenderizarResultados(ResultadosResposta? dados)
    {
        if (_listaResultados == null) return;
        _listaResultados.Children.Clear();
        if (dados == null || dados.Lotes.Count == 0)
        {
            _listaResultados.Children.Add(Texto("Nenhum lote recebido pela loja nesta conta."));
            return;
        }

        foreach (var lote in dados.Lotes)
        {
            var bloco = new VerticalStackLayout
            {
                Spacing = 4,
                Padding = new Thickness(8),
                BackgroundColor = Color.FromArgb("#F1F5F9")
            };
            bloco.Children.Add(new Label
            {
                Text = $"Lote #{lote.Id} - {lote.Situacao}",
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#0F172A")
            });
            if (lote.Situacao == "Cancelado")
                bloco.Children.Add(Texto("Cancelado: " + (lote.MotivoCancelamento ?? "Sem motivo informado.")));
            else if (lote.Situacao == "Recebido")
                bloco.Children.Add(Texto("Aguardando revisao e confirmacao da loja."));
            if (lote.DataConfirmacao.HasValue)
                bloco.Children.Add(Texto($"Confirmacao: {lote.DataConfirmacao.Value:dd/MM/yyyy}"));

            foreach (var pedido in dados.Pedidos.Where(p => p.PedidoGeralId == lote.Id))
            {
                bloco.Children.Add(Texto(
                    $"{pedido.Cliente} - {pedido.Situacao} | {pedido.Condicao}"));
                bloco.Children.Add(Texto(
                    $"Previsto R$ {pedido.TotalPrevisto:N2} | " +
                    (pedido.TotalFinal.HasValue
                        ? $"Final R$ {pedido.TotalFinal.Value:N2}"
                        : "Final: aguardando corte")));
                foreach (var item in dados.Itens.Where(i => i.PedidoId == pedido.Id))
                {
                    var atendido = item.QuantidadeAtendida.HasValue
                        ? item.QuantidadeAtendida.Value.ToString() : "em analise";
                    var cortado = item.QuantidadeCortada.HasValue
                        ? item.QuantidadeCortada.Value.ToString() : "em analise";
                    bloco.Children.Add(Texto(
                        $"{item.Produto}: solicitado {item.QuantidadeSolicitada}, " +
                        $"atendido {atendido}, cortado {cortado}"));
                }
                foreach (var parcela in dados.Parcelas.Where(p => p.PedidoId == pedido.Id))
                {
                    if (parcela.ValorBase.HasValue)
                    {
                        var recebido = parcela.ValorRecebido.HasValue
                            ? $" | Recebido R$ {parcela.ValorRecebido.Value:N2}" : "";
                        bloco.Children.Add(Texto(
                            $"Parcela {parcela.Numero} - {parcela.FormaPagamento}: " +
                            $"R$ {parcela.ValorBase.Value:N2}, vence {parcela.Vencimento:dd/MM/yyyy}, " +
                            $"{parcela.Situacao}{recebido}"));
                    }
                    else
                        bloco.Children.Add(Texto(
                            $"Parcela {parcela.Numero} prevista R$ {parcela.ValorPrevisto:N2} " +
                            $"(D+{parcela.PrazoDias}), sem vencimento definido."));
                }
            }
            _listaResultados.Children.Add(bloco);
        }
    }
}
