using System.Globalization;
using ClickCake.Mobile.Models;

namespace ClickCake.Mobile.Data;

public sealed partial class DadosLocais
{
    public Task<List<PedidoGeralRascunho>> ListarCiclosAsync() =>
        _banco.Table<PedidoGeralRascunho>().OrderByDescending(x => x.CriadoEmUtc).ToListAsync();

    public Task<List<VisitaRascunho>> ListarVisitasAsync(string cicloId) =>
        _banco.Table<VisitaRascunho>().Where(x => x.PedidoGeralId == cicloId)
            .OrderBy(x => x.OrdemVisita).ToListAsync();

    public Task<List<ProdutoLocal>> ListarProdutosCatalogoAsync() =>
        _banco.Table<ProdutoLocal>().OrderBy(x => x.Nome).ToListAsync();

    public Task<List<CategoriaLocal>> ListarCategoriasCatalogoAsync() =>
        _banco.Table<CategoriaLocal>().OrderBy(x => x.Nome).ToListAsync();

    public Task<List<CondicaoLocal>> ListarCondicoesCatalogoAsync() =>
        _banco.Table<CondicaoLocal>().OrderBy(x => x.Id).ToListAsync();

    public Task<List<FormaLocal>> ListarFormasCatalogoAsync() =>
        _banco.Table<FormaLocal>().OrderBy(x => x.Id).ToListAsync();

    public async Task<PedidoGeralRascunho?> BuscarCicloAsync(string cicloId) =>
        await _banco.FindAsync<PedidoGeralRascunho>(cicloId);

    public async Task<string> IniciarCicloAsync()
    {
        var id = Guid.NewGuid().ToString();
        await _banco.RunInTransactionAsync(db =>
        {
            var rota = db.Table<RotaItemLocal>().OrderBy(x => x.OrdemVisita).ToList();
            if (rota.Count == 0) throw new InvalidOperationException("Sincronize uma rota antes de iniciar.");
            var anterior = db.Table<PedidoGeralRascunho>()
                .Where(x => x.Situacao == "EmAndamento").ToList();
            foreach (var rascunho in anterior)
            {
                rascunho.Situacao = "Pausado";
                db.Update(rascunho);
            }
            var configuracao = db.Find<ConfiguracaoLocal>("RotaVersaoId");
            int? versaoId = int.TryParse(configuracao?.Valor, out var numero) ? numero : null;
            db.Insert(new PedidoGeralRascunho
            {
                Id = id,
                IdentificadorEnvio = Guid.NewGuid().ToString(),
                CriadoEmUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                RotaVersaoId = versaoId,
                Situacao = "EmAndamento"
            });
            foreach (var item in rota)
            {
                var cliente = db.Find<ClienteLocal>(item.ClienteId)
                    ?? throw new InvalidOperationException("Cliente da rota nao esta no cache.");
                db.Insert(new VisitaRascunho
                {
                    PedidoGeralId = id,
                    ClienteId = item.ClienteId,
                    ClienteNome = cliente.Nome,
                    CodigoVisita = item.CodigoVisita,
                    OrdemVisita = item.OrdemVisita,
                    Situacao = "Pendente"
                });
            }
        });
        return id;
    }

    public Task RetomarCicloAsync(string cicloId) => _banco.RunInTransactionAsync(db =>
    {
        var escolhido = db.Find<PedidoGeralRascunho>(cicloId)
            ?? throw new InvalidOperationException("Rascunho nao encontrado.");
        if (escolhido.Situacao is not ("EmAndamento" or "Pausado"))
            throw new InvalidOperationException("Este ciclo nao pode ser retomado para atendimento.");
        foreach (var outro in db.Table<PedidoGeralRascunho>()
            .Where(x => x.Situacao == "EmAndamento").ToList())
        {
            outro.Situacao = "Pausado";
            db.Update(outro);
        }
        escolhido.Situacao = "EmAndamento";
        db.Update(escolhido);
    });

    public Task PausarCicloAsync(string cicloId) => _banco.RunInTransactionAsync(db =>
    {
        var ciclo = db.Find<PedidoGeralRascunho>(cicloId)
            ?? throw new InvalidOperationException("Rascunho nao encontrado.");
        if (ciclo.Situacao != "EmAndamento")
            throw new InvalidOperationException("Somente ciclo em andamento pode ser pausado.");
        ciclo.Situacao = "Pausado";
        db.Update(ciclo);
    });

    public Task PularProximaVisitaAsync(string cicloId, string motivo, string? observacao) =>
        _banco.RunInTransactionAsync(db =>
        {
            ValidarPulo(motivo, observacao);
            var ciclo = ExigirEditavel(db, cicloId, apenasAtivo: true);
            var visita = db.Table<VisitaRascunho>()
                .Where(x => x.PedidoGeralId == cicloId && x.Situacao == "Pendente")
                .OrderBy(x => x.OrdemVisita).FirstOrDefault()
                ?? throw new InvalidOperationException("Nao ha visitas pendentes.");
            visita.Situacao = "Pulado";
            visita.MotivoPulo = motivo;
            visita.Observacao = observacao?.Trim();
            db.Update(visita);
            AtualizarSituacao(db, ciclo);
        });

    public Task SalvarPedidoAsync(
        string cicloId, int visitaId, List<ItemCarrinho> itens,
        int condicaoId, List<int> formasPagamento) => _banco.RunInTransactionAsync(db =>
    {
        var ciclo = ExigirEditavel(db, cicloId);
        var visita = db.Find<VisitaRascunho>(visitaId)
            ?? throw new InvalidOperationException("Visita inexistente.");
        if (visita.PedidoGeralId != cicloId || visita.Situacao == "Pulado")
            throw new InvalidOperationException("Visita nao permite pedido.");
        if (visita.Situacao == "Pendente")
        {
            if (ciclo.Situacao != "EmAndamento")
                throw new InvalidOperationException("Retome o ciclo para registrar pedido.");
            var proxima = db.Table<VisitaRascunho>()
                .Where(x => x.PedidoGeralId == cicloId && x.Situacao == "Pendente")
                .OrderBy(x => x.OrdemVisita).FirstOrDefault();
            if (proxima?.Id != visita.Id)
                throw new InvalidOperationException("Atendimento fora da sequencia da rota.");
        }
        if (itens.Count == 0 || itens.Count > 500 ||
            itens.Any(i => i.ProdutoId <= 0 || i.Quantidade <= 0) ||
            itens.Select(i => i.ProdutoId).Distinct().Count() != itens.Count)
            throw new InvalidOperationException("Carrinho invalido ou vazio.");

        var condicao = db.Find<CondicaoLocal>(condicaoId)
            ?? throw new InvalidOperationException("Condicao de pagamento indisponivel.");
        var prazos = condicao.PrazosDias.Split(',').Select(p => int.Parse(p.Trim())).ToArray();
        if (prazos.Length != condicao.QuantidadeParcelas || formasPagamento.Count != prazos.Length ||
            formasPagamento.Any(id => db.Find<FormaLocal>(id) == null))
            throw new InvalidOperationException("Escolha uma forma valida por parcela.");

        decimal subtotal = 0;
        foreach (var item in itens)
        {
            var produto = db.Find<ProdutoLocal>(item.ProdutoId)
                ?? throw new InvalidOperationException($"Produto {item.ProdutoId} nao esta no catalogo.");
            if (item.NomeProduto != produto.Nome ||
                Arredondar(item.PrecoUnitario) != Arredondar(produto.Preco) ||
                produto.DataValidade.Date < DateTime.Today)
                throw new InvalidOperationException($"Produto {item.ProdutoId} exige sincronizacao.");
            subtotal += item.Quantidade * Arredondar(produto.Preco);
        }
        var desconto = Math.Round(condicao.DescontoAVista, 4, MidpointRounding.AwayFromZero);
        var total = Arredondar(subtotal * (1 - desconto));
        if (total > 9999999999m)
            throw new InvalidOperationException("Total acima do limite permitido.");
        var antigo = db.Table<PedidoRascunho>().Where(x => x.VisitaId == visita.Id).FirstOrDefault();
        if (antigo != null)
        {
            db.Execute("DELETE FROM ItemPedidoRascunho WHERE PedidoId=?", antigo.Id);
            db.Execute("DELETE FROM ParcelaRascunho WHERE PedidoId=?", antigo.Id);
        }
        var pedido = antigo ?? new PedidoRascunho
        {
            Id = Guid.NewGuid().ToString(), PedidoGeralId = cicloId, VisitaId = visita.Id
        };
        pedido.CondicaoPagamentoId = condicaoId;
        pedido.TotalPrevisto = total;
        if (antigo == null) db.Insert(pedido); else db.Update(pedido);
        foreach (var item in itens)
            db.Insert(new ItemPedidoRascunho
            {
                PedidoId = pedido.Id, ProdutoId = item.ProdutoId,
                NomeProduto = item.NomeProduto, PrecoUnitario = Arredondar(item.PrecoUnitario),
                Quantidade = item.Quantidade
            });
        decimal acumulado = 0;
        for (var index = 0; index < prazos.Length; index++)
        {
            var valor = index == prazos.Length - 1
                ? total - acumulado : Arredondar(total / prazos.Length);
            acumulado += valor;
            db.Insert(new ParcelaRascunho
            {
                PedidoId = pedido.Id, Numero = index + 1, PrazoDias = prazos[index],
                FormaPagamentoId = formasPagamento[index], ValorPrevisto = valor
            });
        }
        visita.Situacao = "PedidoRegistrado";
        visita.MotivoPulo = null;
        visita.Observacao = null;
        db.Update(visita);
        ciclo.IdentificadorEnvio = Guid.NewGuid().ToString();
        AtualizarSituacao(db, ciclo);
    });

    public Task RemoverPedidoAsync(
        string cicloId, int visitaId, string motivo, string? observacao) =>
        _banco.RunInTransactionAsync(db =>
        {
            ValidarPulo(motivo, observacao);
            var ciclo = ExigirEditavel(db, cicloId);
            var visita = db.Find<VisitaRascunho>(visitaId)
                ?? throw new InvalidOperationException("Visita inexistente.");
            if (visita.PedidoGeralId != cicloId || visita.Situacao != "PedidoRegistrado")
                throw new InvalidOperationException("A visita nao possui pedido removivel.");
            var pedido = db.Table<PedidoRascunho>().Where(x => x.VisitaId == visitaId).FirstOrDefault()
                ?? throw new InvalidOperationException("Pedido nao encontrado.");
            db.Execute("DELETE FROM ItemPedidoRascunho WHERE PedidoId=?", pedido.Id);
            db.Execute("DELETE FROM ParcelaRascunho WHERE PedidoId=?", pedido.Id);
            db.Delete(pedido);
            visita.Situacao = "Pulado";
            visita.MotivoPulo = motivo;
            visita.Observacao = observacao?.Trim();
            db.Update(visita);
            ciclo.IdentificadorEnvio = Guid.NewGuid().ToString();
            AtualizarSituacao(db, ciclo);
        });

    public Task<PedidoEnvio?> BuscarPedidoDaVisitaAsync(int visitaId) => MontarPedidoAsync(visitaId);

    private async Task<PedidoEnvio?> MontarPedidoAsync(int visitaId)
    {
        var pedido = await _banco.Table<PedidoRascunho>().Where(x => x.VisitaId == visitaId).FirstOrDefaultAsync();
        if (pedido == null) return null;
        var itens = await _banco.Table<ItemPedidoRascunho>()
            .Where(x => x.PedidoId == pedido.Id).ToListAsync();
        var parcelas = await _banco.Table<ParcelaRascunho>()
            .Where(x => x.PedidoId == pedido.Id).OrderBy(x => x.Numero).ToListAsync();
        return new PedidoEnvio
        {
            CondicaoPagamentoId = pedido.CondicaoPagamentoId,
            TotalPrevisto = Arredondar(pedido.TotalPrevisto),
            Itens = itens.Select(i => new ItemEnvio
            {
                ProdutoId = i.ProdutoId, NomeProduto = i.NomeProduto,
                PrecoUnitario = Arredondar(i.PrecoUnitario), Quantidade = i.Quantidade
            }).ToList(),
            Parcelas = parcelas.Select(p => new ParcelaEnvio
            {
                Numero = p.Numero, PrazoDias = p.PrazoDias,
                FormaPagamentoId = p.FormaPagamentoId, ValorPrevisto = Arredondar(p.ValorPrevisto)
            }).ToList()
        };
    }

    public async Task<PedidoGeralEnvio> MontarEnvioAsync(string cicloId)
    {
        var ciclo = await BuscarCicloAsync(cicloId)
            ?? throw new InvalidOperationException("Ciclo nao encontrado.");
        if (ciclo.Situacao is not ("ProntoParaEnvio" or "Enviando"))
            throw new InvalidOperationException("Conclua todas as visitas antes do envio.");
        var visitas = await ListarVisitasAsync(cicloId);
        if (visitas.Any(v => v.Situacao == "Pendente") ||
            visitas.All(v => v.Situacao == "Pulado"))
            throw new InvalidOperationException("Lote incompleto ou sem pedidos.");
        var envio = new PedidoGeralEnvio
        {
            IdentificadorEnvio = Guid.Parse(ciclo.IdentificadorEnvio),
            DataCriacaoUtc = DateTime.Parse(ciclo.CriadoEmUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            RotaVersaoId = ciclo.RotaVersaoId
        };
        foreach (var visita in visitas)
        {
            envio.Visitas.Add(new VisitaEnvio
            {
                ClienteId = visita.ClienteId,
                CodigoVisita = visita.CodigoVisita,
                OrdemVisita = visita.OrdemVisita,
                Situacao = visita.Situacao,
                MotivoPulo = visita.MotivoPulo,
                Observacao = visita.Observacao,
                Pedido = await MontarPedidoAsync(visita.Id)
            });
        }
        return envio;
    }

    public Task MarcarEnviandoAsync(string cicloId) => _banco.RunInTransactionAsync(db =>
    {
        var ciclo = db.Find<PedidoGeralRascunho>(cicloId)
            ?? throw new InvalidOperationException("Ciclo nao encontrado.");
        if (ciclo.Situacao is not ("ProntoParaEnvio" or "Enviando"))
            throw new InvalidOperationException("Ciclo nao esta pronto para envio.");
        ciclo.Situacao = "Enviando";
        db.Update(ciclo);
    });

    public Task MarcarEnviadoAsync(string cicloId, ResultadoEnvio resultado) =>
        _banco.RunInTransactionAsync(db =>
        {
            var ciclo = db.Find<PedidoGeralRascunho>(cicloId)
                ?? throw new InvalidOperationException("Ciclo nao encontrado.");
            if (ciclo.IdentificadorEnvio != resultado.IdentificadorEnvio.ToString())
                throw new InvalidOperationException("Identificador da resposta nao confere.");
            ciclo.Situacao = "Enviado";
            ciclo.PedidoGeralServidorId = resultado.PedidoGeralId;
            db.Update(ciclo);
        });

    public Task MarcarRejeitadoAsync(string cicloId) => _banco.RunInTransactionAsync(db =>
    {
        var ciclo = db.Find<PedidoGeralRascunho>(cicloId)
            ?? throw new InvalidOperationException("Ciclo nao encontrado.");
        if (ciclo.Situacao != "Enviando") return;
        ciclo.Situacao = "ProntoParaEnvio";
        db.Update(ciclo);
    });

    private static PedidoGeralRascunho ExigirEditavel(
        SQLite.SQLiteConnection db, string id, bool apenasAtivo = false)
    {
        var ciclo = db.Find<PedidoGeralRascunho>(id)
            ?? throw new InvalidOperationException("Ciclo nao encontrado.");
        if (apenasAtivo ? ciclo.Situacao != "EmAndamento" :
            ciclo.Situacao is not ("EmAndamento" or "ProntoParaEnvio"))
            throw new InvalidOperationException("Este ciclo nao permite alteracoes.");
        return ciclo;
    }

    private static void AtualizarSituacao(SQLite.SQLiteConnection db, PedidoGeralRascunho ciclo)
    {
        var visitas = db.Table<VisitaRascunho>()
            .Where(x => x.PedidoGeralId == ciclo.Id).ToList();
        if (visitas.All(x => x.Situacao != "Pendente"))
            ciclo.Situacao = visitas.All(x => x.Situacao == "Pulado")
                ? "EncerradoSemPedidos" : "ProntoParaEnvio";
        db.Update(ciclo);
    }

    private static void ValidarPulo(string motivo, string? observacao)
    {
        if (motivo is not ("NaoFaraPedido" or "LocalFechado" or "Indisponivel" or "Outro") ||
            (motivo == "Outro" && string.IsNullOrWhiteSpace(observacao)) ||
            observacao?.Length > 500)
            throw new InvalidOperationException("Informe motivo valido e observacao obrigatoria para Outro.");
    }

    private static decimal Arredondar(decimal valor) =>
        Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
