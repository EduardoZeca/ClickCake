using System.Globalization;
using ClickCake.Mobile.Models;
using ClickCake.Mobile.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ClickCake.Mobile;

public partial class MainPage
{
    private string? _cicloAtualId;
    private int? _visitaEdicaoId;
    private readonly List<ItemCarrinho> _carrinho = new();
    private readonly List<Picker> _pickersFormas = new();
    private readonly List<Label> _valoresParcelas = new();
    private List<CategoriaLocal> _catalogoCategorias = new();
    private List<ProdutoLocal> _catalogoProdutos = new();
    private List<CondicaoLocal> _catalogoCondicoes = new();
    private List<FormaLocal> _catalogoFormas = new();

    private async Task AtualizarCatalogosPedidoAsync()
    {
        if (_dadosLocais == null) return;
        _catalogoCategorias = await _dadosLocais.ListarCategoriasCatalogoAsync();
        _catalogoProdutos = await _dadosLocais.ListarProdutosCatalogoAsync();
        _catalogoCondicoes = await _dadosLocais.ListarCondicoesCatalogoAsync();
        _catalogoFormas = await _dadosLocais.ListarFormasCatalogoAsync();
        EscolhaCategoria.ItemsSource = _catalogoCategorias;
        EscolhaCategoria.ItemDisplayBinding = new Binding(nameof(CategoriaLocal.Nome));
        EscolhaProduto.ItemsSource = _catalogoProdutos;
        EscolhaProduto.ItemDisplayBinding = new Binding(nameof(ProdutoLocal.Nome));
        EscolhaCondicao.ItemsSource = _catalogoCondicoes;
        EscolhaCondicao.ItemDisplayBinding = new Binding(nameof(CondicaoLocal.Nome));
        RenderizarCarrinho();
    }

    private async Task AtualizarListaCiclosAsync()
    {
        ListaCiclos.Children.Clear();
        if (_dadosLocais == null) return;
        var ciclos = await _dadosLocais.ListarCiclosAsync();
        if (ciclos.Count == 0)
        {
            ListaCiclos.Children.Add(Texto("Nenhum ciclo criado. Sincronize a rota e inicie um ciclo."));
            return;
        }
        foreach (var ciclo in ciclos)
        {
            var id = ciclo.Id;
            var data = DateTime.Parse(ciclo.CriadoEmUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind).ToLocalTime();
            var painel = new VerticalStackLayout { Spacing = 4, Padding = new Thickness(6) };
            painel.Children.Add(Texto($"{data:dd/MM HH:mm} - {ciclo.Situacao}"));
            var botoes = new HorizontalStackLayout { Spacing = 8 };
            var visualizar = Botao("Ver resumo", "#475569");
            visualizar.Clicked += async (_, _) => await ExecutarPedidoUiAsync(async () =>
            {
                _cicloAtualId = id;
                ReiniciarFormulario();
                await ExibirCicloAsync();
            });
            botoes.Children.Add(visualizar);
            if (ciclo.Situacao == "Pausado")
            {
                var retomar = Botao("Retomar", "#047857");
                retomar.Clicked += async (_, _) => await ExecutarPedidoUiAsync(async () =>
                {
                    await _dadosLocais.RetomarCicloAsync(id);
                    _cicloAtualId = id;
                    ReiniciarFormulario();
                    await AtualizarListaCiclosAsync();
                    await ExibirCicloAsync();
                });
                botoes.Children.Add(retomar);
            }
            painel.Children.Add(botoes);
            ListaCiclos.Children.Add(painel);
        }
    }

    private async Task ExibirCicloAsync()
    {
        if (_dadosLocais == null || string.IsNullOrWhiteSpace(_cicloAtualId))
        {
            AreaCiclo.IsVisible = false;
            return;
        }
        var ciclo = await _dadosLocais.BuscarCicloAsync(_cicloAtualId);
        if (ciclo == null)
        {
            AreaCiclo.IsVisible = false;
            return;
        }
        AreaCiclo.IsVisible = true;
        CicloSelecionado.Text = $"Ciclo {ciclo.Id[..8]} - {ciclo.Situacao}";
        BotaoPausarCiclo.IsVisible = ciclo.Situacao == "EmAndamento";
        BotaoEnviarLote.IsVisible = ciclo.Situacao is "ProntoParaEnvio" or "Enviando";
        var visitas = await _dadosLocais.ListarVisitasAsync(ciclo.Id);
        var pendente = visitas.FirstOrDefault(v => v.Situacao == "Pendente");
        var atendidas = visitas.Count(v => v.Situacao != "Pendente");
        MensagemCicloAtual.Text = $"{atendidas}/{visitas.Count} visitas concluidas. " +
            (ciclo.Situacao == "Enviando" ? "Envio pendente de consulta na API." : "");
        AreaAtendimento.IsVisible = ciclo.Situacao == "EmAndamento" && pendente != null;
        BotaoPularVisita.IsVisible = AreaAtendimento.IsVisible;
        if (pendente != null)
            ProximaVisita.Text = $"Proximo: {pendente.OrdemVisita}. {pendente.ClienteNome} " +
                $"(codigo {pendente.CodigoVisita})";
        await AtualizarHistoricoClienteAsync(pendente?.ClienteId);

        ListaResumo.Children.Clear();
        decimal valorGeral = 0;
        foreach (var visita in visitas)
        {
            var pedido = await _dadosLocais.BuscarPedidoDaVisitaAsync(visita.Id);
            var descricao = $"{visita.OrdemVisita}. {visita.ClienteNome}: {visita.Situacao}";
            if (visita.Situacao == "Pulado")
                descricao += $" - {visita.MotivoPulo} {visita.Observacao}";
            var bloco = new VerticalStackLayout { Spacing = 3, Padding = new Thickness(4) };
            bloco.Children.Add(Texto(descricao));
            if (pedido != null)
            {
                valorGeral += pedido.TotalPrevisto;
                bloco.Children.Add(Texto(string.Join(", ", pedido.Itens.Select(i =>
                    $"{i.NomeProduto} x{i.Quantidade}")) + $" | Previsto R$ {pedido.TotalPrevisto:N2}"));
                bloco.Children.Add(Texto(string.Join(" | ", pedido.Parcelas.Select(p =>
                    $"Parcela {p.Numero}: R$ {p.ValorPrevisto:N2} D+{p.PrazoDias}"))));
                if (ciclo.Situacao is "EmAndamento" or "ProntoParaEnvio")
                {
                    var idVisita = visita.Id;
                    var botoes = new HorizontalStackLayout { Spacing = 8 };
                    var editar = Botao("Editar", "#1D4ED8");
                    editar.Clicked += async (_, _) => await ExecutarPedidoUiAsync(
                        () => CarregarEdicaoAsync(idVisita));
                    var remover = Botao("Remover (com motivo)", "#B45309");
                    remover.Clicked += async (_, _) => await ExecutarPedidoUiAsync(async () =>
                    {
                        var (motivo, observacao) = MotivoSelecionado();
                        await _dadosLocais.RemoverPedidoAsync(ciclo.Id, idVisita, motivo, observacao);
                        _visitaEdicaoId = null;
                        await AtualizarListaCiclosAsync();
                        await ExibirCicloAsync();
                    });
                    botoes.Children.Add(editar);
                    botoes.Children.Add(remover);
                    bloco.Children.Add(botoes);
                }
            }
            ListaResumo.Children.Add(bloco);
        }
        ListaResumo.Children.Add(Texto($"Total previsto do lote: R$ {valorGeral:N2}"));
        if (ciclo.Situacao == "EncerradoSemPedidos")
            ListaResumo.Children.Add(Texto("Ciclo encerrado localmente sem pedidos. Nada sera enviado."));
    }

    private async Task CarregarEdicaoAsync(int visitaId)
    {
        if (_dadosLocais == null) return;
        var visitas = await _dadosLocais.ListarVisitasAsync(_cicloAtualId!);
        var visita = visitas.Single(v => v.Id == visitaId);
        var pedido = await _dadosLocais.BuscarPedidoDaVisitaAsync(visitaId)
            ?? throw new InvalidOperationException("Pedido nao encontrado.");
        _visitaEdicaoId = visitaId;
        _carrinho.Clear();
        foreach (var item in pedido.Itens)
            _carrinho.Add(new ItemCarrinho
            {
                ProdutoId = item.ProdutoId, NomeProduto = item.NomeProduto,
                PrecoUnitario = item.PrecoUnitario, Quantidade = item.Quantidade
            });
        AreaAtendimento.IsVisible = true;
        BotaoPularVisita.IsVisible = false;
        ProximaVisita.Text = $"Editando pedido de {visita.ClienteNome}";
        await AtualizarHistoricoClienteAsync(visita.ClienteId);
        EscolhaCondicao.SelectedItem = _catalogoCondicoes
            .FirstOrDefault(c => c.Id == pedido.CondicaoPagamentoId);
        for (var i = 0; i < pedido.Parcelas.Count && i < _pickersFormas.Count; i++)
            _pickersFormas[i].SelectedItem = _catalogoFormas
                .FirstOrDefault(f => f.Id == pedido.Parcelas[i].FormaPagamentoId);
        BotaoSalvarPedido.Text = "Salvar alteracoes do pedido";
        RenderizarCarrinho();
    }

    private async void AoNovoCiclo(object? sender, EventArgs e) =>
        await ExecutarPedidoUiAsync(async () =>
        {
            if (_dadosLocais == null) return;
            _cicloAtualId = await _dadosLocais.IniciarCicloAsync();
            ReiniciarFormulario();
            await AtualizarListaCiclosAsync();
            await ExibirCicloAsync();
        });

    private async void AoPausarCiclo(object? sender, EventArgs e) =>
        await ExecutarPedidoUiAsync(async () =>
        {
            await _dadosLocais!.PausarCicloAsync(_cicloAtualId!);
            ReiniciarFormulario();
            await AtualizarListaCiclosAsync();
            await ExibirCicloAsync();
        });

    private void AoAdicionarItem(object? sender, EventArgs e)
    {
        if (EscolhaProduto.SelectedItem is not ProdutoLocal produto ||
            !int.TryParse(EntradaQuantidade.Text, out var quantidade) || quantidade <= 0)
        {
            MensagemCiclos.Text = "Selecione produto e quantidade inteira positiva.";
            return;
        }
        var item = _carrinho.FirstOrDefault(i => i.ProdutoId == produto.Id);
        if (item == null)
            _carrinho.Add(new ItemCarrinho
            {
                ProdutoId = produto.Id, NomeProduto = produto.Nome,
                PrecoUnitario = Math.Round(produto.Preco, 2, MidpointRounding.AwayFromZero),
                Quantidade = quantidade
            });
        else
        {
            if (item.Quantidade > int.MaxValue - quantidade)
            {
                MensagemCiclos.Text = "Quantidade acima do limite.";
                return;
            }
            item.Quantidade += quantidade;
        }
        EntradaQuantidade.Text = "";
        RenderizarCarrinho();
    }

    private void AoEscolherCategoria(object? sender, EventArgs e)
    {
        var categoriaId = (EscolhaCategoria.SelectedItem as CategoriaLocal)?.Id;
        EscolhaProduto.ItemsSource = categoriaId == null
            ? _catalogoProdutos
            : _catalogoProdutos.Where(p => p.CategoriaId == categoriaId).ToList();
        EscolhaProduto.SelectedIndex = -1;
    }

    private void RenderizarCarrinho()
    {
        ListaCarrinho.Children.Clear();
        foreach (var item in _carrinho)
        {
            var produtoId = item.ProdutoId;
            var linha = new HorizontalStackLayout { Spacing = 6 };
            linha.Children.Add(Texto(item.Resumo));
            var retirar = Botao("Retirar", "#B45309");
            retirar.Clicked += (_, _) =>
            {
                _carrinho.RemoveAll(x => x.ProdutoId == produtoId);
                RenderizarCarrinho();
            };
            linha.Children.Add(retirar);
            ListaCarrinho.Children.Add(linha);
        }
        var subtotal = _carrinho.Sum(i => i.Subtotal);
        var desconto = Math.Round(
            (EscolhaCondicao.SelectedItem as CondicaoLocal)?.DescontoAVista ?? 0,
            4, MidpointRounding.AwayFromZero);
        var previsto = Math.Round(subtotal * (1 - desconto), 2, MidpointRounding.AwayFromZero);
        TotalCarrinho.Text = $"Subtotal R$ {subtotal:N2} | Total previsto R$ {previsto:N2}";
        decimal distribuido = 0;
        for (var i = 0; i < _valoresParcelas.Count; i++)
        {
            var valor = i == _valoresParcelas.Count - 1
                ? previsto - distribuido
                : Math.Round(previsto / _valoresParcelas.Count, 2, MidpointRounding.AwayFromZero);
            distribuido += valor;
            _valoresParcelas[i].Text = $"Parcela {i + 1} prevista: R$ {valor:N2}";
        }
    }

    private void AoEscolherCondicao(object? sender, EventArgs e)
    {
        FormasParcelas.Children.Clear();
        _pickersFormas.Clear();
        _valoresParcelas.Clear();
        if (EscolhaCondicao.SelectedItem is CondicaoLocal condicao)
        {
            var prazos = condicao.PrazosDias.Split(',');
            for (var i = 0; i < prazos.Length; i++)
            {
                var valor = Texto("");
                _valoresParcelas.Add(valor);
                FormasParcelas.Children.Add(valor);
                var picker = new Picker
                {
                    Title = $"Forma da parcela {i + 1} (D+{prazos[i].Trim()})",
                    TextColor = Colors.Black,
                    ItemsSource = _catalogoFormas,
                    ItemDisplayBinding = new Binding(nameof(FormaLocal.Nome))
                };
                EstilizarSeletor(picker);
                _pickersFormas.Add(picker);
                FormasParcelas.Children.Add(picker);
            }
        }
        RenderizarCarrinho();
    }

    private async void AoSalvarPedido(object? sender, EventArgs e) =>
        await ExecutarPedidoUiAsync(async () =>
        {
            if (_dadosLocais == null || _cicloAtualId == null) return;
            if (EscolhaCondicao.SelectedItem is not CondicaoLocal condicao ||
                _pickersFormas.Count == 0 ||
                _pickersFormas.Any(p => p.SelectedItem is not FormaLocal))
                throw new InvalidOperationException("Escolha a condicao e a forma de cada parcela.");
            var visitaId = _visitaEdicaoId;
            if (visitaId == null)
            {
                var visitas = await _dadosLocais.ListarVisitasAsync(_cicloAtualId);
                visitaId = visitas.FirstOrDefault(v => v.Situacao == "Pendente")?.Id;
            }
            if (visitaId == null)
                throw new InvalidOperationException("Nao ha visita para receber o pedido.");
            await _dadosLocais.SalvarPedidoAsync(
                _cicloAtualId, visitaId.Value, _carrinho.ToList(), condicao.Id,
                _pickersFormas.Select(p => ((FormaLocal)p.SelectedItem!).Id).ToList());
            ReiniciarFormulario();
            await AtualizarListaCiclosAsync();
            await ExibirCicloAsync();
            MensagemCiclos.Text = "Pedido salvo no SQLite, sem enviar nem reservar estoque.";
        });

    private async void AoPularVisita(object? sender, EventArgs e) =>
        await ExecutarPedidoUiAsync(async () =>
        {
            var (motivo, observacao) = MotivoSelecionado();
            await _dadosLocais!.PularProximaVisitaAsync(_cicloAtualId!, motivo, observacao);
            ReiniciarFormulario();
            await AtualizarListaCiclosAsync();
            await ExibirCicloAsync();
        });

    private (string Motivo, string? Observacao) MotivoSelecionado()
    {
        var motivos = new[] { "NaoFaraPedido", "LocalFechado", "Indisponivel", "Outro" };
        if (EscolhaMotivo.SelectedIndex < 0)
            throw new InvalidOperationException("Escolha o motivo do pulo/remocao.");
        var motivo = motivos[EscolhaMotivo.SelectedIndex];
        var observacao = EntradaObservacao.Text?.Trim();
        if (motivo == "Outro" && string.IsNullOrWhiteSpace(observacao))
            throw new InvalidOperationException("Descreva o motivo Outro.");
        return (motivo, observacao);
    }

    private async void AoEnviarLote(object? sender, EventArgs e) =>
        await ExecutarPedidoUiAsync(async () =>
        {
            if (_dadosLocais == null || _cicloAtualId == null || string.IsNullOrEmpty(_token)) return;
            BotaoEnviarLote.IsEnabled = false;
            try
            {
                var envio = await _dadosLocais.MontarEnvioAsync(_cicloAtualId);
                await _dadosLocais.MarcarEnviandoAsync(_cicloAtualId);
                // Consulta primeiro: evita duplicar quando a resposta anterior se perdeu.
                var resultado = await _api.ConsultarEnvioAsync(_token, envio.IdentificadorEnvio)
                    ?? await _api.EnviarPedidoGeralAsync(_token, envio);
                await _dadosLocais.MarcarEnviadoAsync(_cicloAtualId, resultado);
                MensagemCiclos.Text = $"Lote recebido pela loja: #{resultado.PedidoGeralId} ({resultado.Situacao}).";
            }
            catch (RejeicaoEnvioException ex)
            {
                await _dadosLocais.MarcarRejeitadoAsync(_cicloAtualId);
                MensagemCiclos.Text = $"Lote rejeitado: {ex.Message}. Corrija o pedido e envie novamente.";
            }
            catch (SessaoExpiradaException)
            {
                await EncerrarSessaoAsync();
                MensagemLogin.Text = "Sessao expirada. Entre novamente para verificar o envio.";
                return;
            }
            catch (Exception ex)
            {
                // O estado Enviando fica salvo. Novo clique consultara a API com o MESMO GUID.
                MensagemCiclos.Text = $"Resultado desconhecido: {ex.Message}. Dados preservados. Tente verificar novamente.";
            }
            finally
            {
                BotaoEnviarLote.IsEnabled = true;
            }
            await AtualizarListaCiclosAsync();
            await ExibirCicloAsync();
        });

    private void ReiniciarFormulario()
    {
        _visitaEdicaoId = null;
        _carrinho.Clear();
        EscolhaCategoria.SelectedIndex = -1;
        EscolhaProduto.SelectedIndex = -1;
        EscolhaCondicao.SelectedIndex = -1;
        EscolhaMotivo.SelectedIndex = -1;
        EntradaQuantidade.Text = "";
        EntradaObservacao.Text = "";
        BotaoSalvarPedido.Text = "Acrescentar pedido a lista";
        RenderizarCarrinho();
    }

    private async Task ExecutarPedidoUiAsync(Func<Task> trabalho)
    {
        try
        {
            MensagemCiclos.Text = "";
            await trabalho();
        }
        catch (Exception ex)
        {
            MensagemCiclos.Text = ex.Message;
        }
    }

    private static Label Texto(string conteudo) => new()
    {
        Text = conteudo, TextColor = Color.FromArgb("#1E293B"),
        FontSize = 13, LineBreakMode = LineBreakMode.WordWrap,
        VerticalTextAlignment = TextAlignment.Center
    };

    private static Button Botao(string texto, string cor) => new()
    {
        Text = texto, BackgroundColor = Color.FromArgb(cor),
        TextColor = Colors.White, FontSize = 12, Padding = new Thickness(10, 5)
    };
}
