using System.Data;
using System.Globalization;
using ClickCake.Api.Data;
using ClickCake.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickCake.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrador")]
[Route("api/admin/pedidosgerais")]
public sealed class PedidosAdminController(Banco banco) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? situacao, [FromQuery] int? vendedorId,
        [FromQuery] DateTime? inicio, [FromQuery] DateTime? fim)
    {
        if (!string.IsNullOrWhiteSpace(situacao) &&
            situacao is not ("Recebido" or "Confirmado" or "Cancelado"))
            return BadRequest(new { mensagem = "Situacao invalida." });
        if (vendedorId is <= 0 || (inicio.HasValue && fim.HasValue && inicio > fim))
            return BadRequest(new { mensagem = "Filtros invalidos." });

        var sql = """
            SELECT g.Id, g.IdentificadorEnvio, g.VendedorId,
                   u.Nome AS Vendedor, g.DataEnvio, g.DataConfirmacao, g.Situacao,
                   (SELECT COUNT(*) FROM Pedido p WHERE p.PedidoGeralId=g.Id) AS QuantidadePedidos,
                   COALESCE((SELECT SUM(p.TotalPrevisto) FROM Pedido p
                             WHERE p.PedidoGeralId=g.Id),0) AS TotalPrevisto,
                   COALESCE((SELECT SUM(v.TotalFinal) FROM Venda v JOIN Pedido p ON p.Id=v.PedidoId
                             WHERE p.PedidoGeralId=g.Id),0) AS TotalConfirmado
            FROM PedidoGeral g JOIN Usuario u ON u.Id=g.VendedorId
            WHERE (@Situacao IS NULL OR g.Situacao=@Situacao)
              AND (@VendedorId IS NULL OR g.VendedorId=@VendedorId)
              AND (@Inicio IS NULL OR g.DataEnvio>=@Inicio)
              AND (@Fim IS NULL OR g.DataEnvio<DATE_ADD(@Fim, INTERVAL 1 DAY))
            ORDER BY g.DataEnvio DESC,g.Id DESC
            """;
        return Ok(await banco.ConsultarAsync(sql, new Dictionary<string, object?>
        {
            ["Situacao"] = situacao,
            ["VendedorId"] = vendedorId,
            ["Inicio"] = inicio?.Date,
            ["Fim"] = fim?.Date
        }));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detalhar(int id)
    {
        if (id <= 0) return BadRequest();
        await using var conexao = banco.Abrir();
        await conexao.OpenAsync();
        var detalhe = await CarregarDetalhe(conexao, null, id);
        return detalhe is null ? NotFound(new { mensagem = "Lote nao encontrado." }) : Ok(detalhe);
    }

    [HttpGet("/api/admin/vendas")]
    public async Task<IActionResult> Vendas()
    {
        return Ok(await banco.ConsultarAsync("""
            SELECT v.Id, v.PedidoId, p.PedidoGeralId, g.VendedorId,
                   u.Nome AS Vendedor, p.ClienteId, c.Nome AS Cliente,
                   p.Situacao, v.DataConfirmacao, v.TotalFinal,
                   COALESCE((SELECT SUM(i.QuantidadeSolicitada) FROM ItemPedido i
                             WHERE i.PedidoId=p.Id),0) AS UnidadesSolicitadas,
                   COALESCE((SELECT SUM(i.QuantidadeAtendida) FROM ItemPedido i
                             WHERE i.PedidoId=p.Id),0) AS UnidadesAtendidas
            FROM Venda v JOIN Pedido p ON p.Id=v.PedidoId
            JOIN PedidoGeral g ON g.Id=p.PedidoGeralId
            JOIN Usuario u ON u.Id=g.VendedorId
            JOIN Cliente c ON c.Id=p.ClienteId
            ORDER BY v.DataConfirmacao DESC,v.Id DESC
            """));
    }

    [HttpPost("{id:int}/cancelar")]
    public async Task<IActionResult> Cancelar(int id, [FromBody] CancelamentoLoteDto? dto)
    {
        var motivo = dto?.Motivo?.Trim();
        if (id <= 0 || string.IsNullOrWhiteSpace(motivo) || motivo.Length > 500)
            return BadRequest(new { mensagem = "Informe motivo de cancelamento (ate 500 caracteres)." });

        await using var conexao = banco.Abrir();
        await conexao.OpenAsync();
        await using var transacao = await conexao.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var lotes = await Consultar(conexao, transacao,
            "SELECT Situacao FROM PedidoGeral WHERE Id=@Id FOR UPDATE",
            new() { ["Id"] = id });
        if (lotes.Count == 0) return NotFound(new { mensagem = "Lote nao encontrado." });
        if (Texto(lotes[0]["Situacao"]) != "Recebido")
            return Conflict(new { mensagem = "Somente lotes recebidos podem ser cancelados." });

        await Executar(conexao, transacao, """
            UPDATE PedidoGeral SET Situacao='Cancelado',MotivoCancelamento=@Motivo
            WHERE Id=@Id
            """, new() { ["Id"] = id, ["Motivo"] = motivo });
        await Executar(conexao, transacao,
            "UPDATE Pedido SET Situacao='Cancelado' WHERE PedidoGeralId=@Id",
            new() { ["Id"] = id });
        await transacao.CommitAsync();
        return Ok(new { mensagem = "Lote cancelado sem movimentar estoque.", pedidoGeralId = id });
    }

    [HttpPost("{id:int}/confirmar")]
    public async Task<IActionResult> Confirmar(int id, [FromBody] ConfirmacaoCorteDto? dto)
    {
        if (id <= 0 || dto?.Itens is null || dto.Itens.Any(i => i is null ||
            i.ItemPedidoId <= 0 || i.QuantidadeAtendida < 0))
            return BadRequest(new { mensagem = "Lista de cortes invalida." });

        await using var conexao = banco.Abrir();
        await conexao.OpenAsync();
        await using var transacao = await conexao.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            // Trava o lote: duas confirmacoes concorrentes nao podem criar duas vendas.
            var lotes = await Consultar(conexao, transacao,
                "SELECT Situacao FROM PedidoGeral WHERE Id=@Id FOR UPDATE",
                new() { ["Id"] = id });
            if (lotes.Count == 0) return NotFound(new { mensagem = "Lote nao encontrado." });
            var situacao = Texto(lotes[0]["Situacao"]);
            if (situacao == "Confirmado")
                return Ok(new { mensagem = "Lote ja estava confirmado.", pedidoGeralId = id, repetido = true });
            if (situacao != "Recebido")
                return Conflict(new { mensagem = "Lote cancelado nao pode ser confirmado." });

            var itens = await CarregarItens(conexao, transacao, id);
            if (itens.Count == 0 || dto.Itens.Count != itens.Count ||
                dto.Itens.Select(i => i.ItemPedidoId).Distinct().Count() != itens.Count)
                return BadRequest(new { mensagem = "Envie exatamente um corte por item do lote." });

            var quantidades = dto.Itens.ToDictionary(i => i.ItemPedidoId, i => i.QuantidadeAtendida);
            foreach (var item in itens)
            {
                var itemId = Inteiro(item["Id"]);
                if (!quantidades.TryGetValue(itemId, out var qtd) ||
                    qtd > Inteiro(item["QuantidadeSolicitada"]))
                    return BadRequest(new { mensagem = $"Quantidade invalida para item #{itemId}." });
            }

            var hojeUtc = DateTime.UtcNow.Date;
            var estoqueBloqueado = new Dictionary<int, long>();
            foreach (var produtoId in itens.Select(i => Inteiro(i["ProdutoId"])).Distinct().Order())
            {
                // Travar produtos em ordem fixa evita consumir saldo concorrente.
                var produto = await Consultar(conexao, transacao, """
                    SELECT Estoque,DataValidade,Ativo FROM Produto
                    WHERE Id=@Id FOR UPDATE
                    """, new() { ["Id"] = produtoId });
                if (produto.Count == 0)
                    throw new RegraCorteException($"Produto #{produtoId} nao encontrado.");

                var p = produto[0];
                estoqueBloqueado[produtoId] =
                    Convert.ToBoolean(p["Ativo"], CultureInfo.InvariantCulture) &&
                    Convert.ToDateTime(p["DataValidade"], CultureInfo.InvariantCulture).Date >= hojeUtc
                    ? Longo(p["Estoque"]) : 0;
            }

            var consumo = new Dictionary<int, long>();
            foreach (var item in itens)
            {
                var produtoId = Inteiro(item["ProdutoId"]);
                consumo[produtoId] = consumo.GetValueOrDefault(produtoId) +
                    quantidades[Inteiro(item["Id"])];
            }
            foreach (var (produtoId, total) in consumo)
                if (total > estoqueBloqueado[produtoId])
                    throw new RegraCorteException(
                        $"Estoque ou validade do produto #{produtoId} mudou. Revise o corte.");

            // Primeiro grava cortes e efetua a baixa, ainda dentro da mesma transacao.
            foreach (var item in itens)
                await Executar(conexao, transacao, """
                    UPDATE ItemPedido SET QuantidadeAtendida=@Quantidade WHERE Id=@Id
                    """, new()
                {
                    ["Id"] = Inteiro(item["Id"]),
                    ["Quantidade"] = quantidades[Inteiro(item["Id"])]
                });

            foreach (var (produtoId, total) in consumo.Where(x => x.Value > 0).OrderBy(x => x.Key))
            {
                var alteradas = await Executar(conexao, transacao, """
                    UPDATE Produto SET Estoque=Estoque-@Quantidade
                    WHERE Id=@Id AND Estoque>=@Quantidade
                      AND Ativo=TRUE AND DataValidade>=@Hoje
                    """, new()
                {
                    ["Id"] = produtoId, ["Quantidade"] = total, ["Hoje"] = hojeUtc
                });
                if (alteradas != 1)
                    throw new RegraCorteException($"Saldo do produto #{produtoId} foi alterado. Revise.");
            }

            var pedidos = await CarregarPedidos(conexao, transacao, id);
            var parcelas = await CarregarParcelas(conexao, transacao, id);
            var agoraUtc = DateTime.UtcNow;
            var vendas = 0;

            foreach (var pedido in pedidos)
            {
                var pedidoId = Inteiro(pedido["Id"]);
                var itensPedido = itens.Where(i => Inteiro(i["PedidoId"]) == pedidoId).ToList();
                var todosAtendidos = itensPedido.All(i =>
                    quantidades[Inteiro(i["Id"])] == Inteiro(i["QuantidadeSolicitada"]));
                var existemAtendidos = itensPedido.Any(i => quantidades[Inteiro(i["Id"])] > 0);
                var situacaoPedido = !existemAtendidos ? "NaoAtendido" :
                    todosAtendidos ? "AtendidoTotal" : "AtendidoParcial";

                decimal bruto = 0m;
                foreach (var item in itensPedido)
                    bruto += Decimal(item["PrecoUnitarioSnapshot"]) *
                        quantidades[Inteiro(item["Id"])];
                var desconto = Decimal(pedido["DescontoAVista"]);
                var final = Arredondar(bruto * (1m - desconto));
                if (final > 9999999999.99m)
                    throw new RegraCorteException($"Valor excedido no pedido #{pedidoId}.");

                await Executar(conexao, transacao, """
                    UPDATE Pedido SET Situacao=@Situacao,TotalFinal=@TotalFinal WHERE Id=@Id
                    """, new()
                {
                    ["Id"] = pedidoId, ["Situacao"] = situacaoPedido, ["TotalFinal"] = final
                });

                if (!existemAtendidos) continue;
                // Mesmo que final seja zero, existe venda se houve unidades atendidas.
                var vendaId = await Inserir(conexao, transacao, """
                    INSERT INTO Venda (PedidoId,DataConfirmacao,TotalFinal)
                    VALUES (@PedidoId,@Data,@Total)
                    """, new()
                {
                    ["PedidoId"] = pedidoId, ["Data"] = agoraUtc, ["Total"] = final
                });
                vendas++;

                foreach (var item in itensPedido)
                {
                    var atendida = quantidades[Inteiro(item["Id"])];
                    if (atendida == 0) continue;
                    await Inserir(conexao, transacao, """
                        INSERT INTO ItemVenda (VendaId,ProdutoId,Quantidade,PrecoUnitario)
                        VALUES (@Venda,@Produto,@Quantidade,@Preco)
                        """, new()
                    {
                        ["Venda"] = vendaId, ["Produto"] = Inteiro(item["ProdutoId"]),
                        ["Quantidade"] = atendida,
                        ["Preco"] = Decimal(item["PrecoUnitarioSnapshot"])
                    });
                }

                var parcelasPedido = parcelas.Where(p => Inteiro(p["PedidoId"]) == pedidoId)
                    .OrderBy(p => Inteiro(p["Numero"])).ToList();
                var quantidadeParcelas = Inteiro(pedido["QuantidadeParcelas"]);
                if (parcelasPedido.Count != quantidadeParcelas ||
                    !parcelasPedido.Select(p => Inteiro(p["Numero"]))
                        .SequenceEqual(Enumerable.Range(1, quantidadeParcelas)))
                    throw new RegraCorteException($"Parcelas incompletas no pedido #{pedidoId}.");

                var centavos = decimal.ToInt64(final * 100m);
                var parcelaBase = centavos / quantidadeParcelas;
                for (var i = 0; i < parcelasPedido.Count; i++)
                {
                    var parcela = parcelasPedido[i];
                    var valorCentavos = i == parcelasPedido.Count - 1
                        ? centavos - (parcelaBase * (quantidadeParcelas - 1))
                        : parcelaBase;
                    await Inserir(conexao, transacao, """
                        INSERT INTO ContaReceber
                          (VendaId,ParcelaPedidoId,FormaPagamentoId,NumeroParcela,
                           Vencimento,ValorBase,Situacao)
                        VALUES (@Venda,@Parcela,@Forma,@Numero,@Vencimento,@Valor,'Aberta')
                        """, new()
                    {
                        ["Venda"] = vendaId, ["Parcela"] = Inteiro(parcela["Id"]),
                        ["Forma"] = Inteiro(parcela["FormaPagamentoId"]),
                        ["Numero"] = Inteiro(parcela["Numero"]),
                        ["Vencimento"] = agoraUtc.Date.AddDays(Inteiro(parcela["PrazoDias"])),
                        ["Valor"] = valorCentavos / 100m
                    });
                }
            }

            await Executar(conexao, transacao, """
                UPDATE PedidoGeral SET Situacao='Confirmado',DataConfirmacao=@Agora WHERE Id=@Id
                """, new() { ["Id"] = id, ["Agora"] = agoraUtc });
            await transacao.CommitAsync();
            return Ok(new
            {
                mensagem = "Lote confirmado. Estoque, vendas e contas a receber gravados.",
                pedidoGeralId = id, vendasCriadas = vendas, repetido = false
            });
        }
        catch (RegraCorteException ex)
        {
            await transacao.RollbackAsync();
            return Conflict(new { mensagem = ex.Message });
        }
    }

    private static async Task<object?> CarregarDetalhe(MySqlConnection c, MySqlTransaction? t, int id)
    {
        var lotes = await Consultar(c, t, """
            SELECT g.Id,g.IdentificadorEnvio,g.Situacao,g.DataEnvio,
                   g.DataConfirmacao,g.MotivoCancelamento,u.Nome AS Vendedor
            FROM PedidoGeral g JOIN Usuario u ON u.Id=g.VendedorId
            WHERE g.Id=@Id
            """, new() { ["Id"] = id });
        if (lotes.Count == 0) return null;
        var visitas = await Consultar(c, t, """
            SELECT v.OrdemSnapshot,v.CodigoVisitaSnapshot,v.Situacao,
                   v.MotivoPulo,v.Observacao,cl.Nome AS Cliente
            FROM Visita v JOIN Cliente cl ON cl.Id=v.ClienteId
            WHERE v.PedidoGeralId=@Id ORDER BY v.OrdemSnapshot
            """, new() { ["Id"] = id });
        var pedidos = await CarregarPedidos(c, t, id);
        var itens = await CarregarItens(c, t, id);
        var parcelas = await CarregarParcelas(c, t, id);
        var contas = await Consultar(c, t, """
            SELECT cr.Id,v.PedidoId,cr.NumeroParcela,fp.Nome AS FormaPagamento,
                   cr.Vencimento,cr.ValorBase,cr.Situacao,cr.DataRecebimento,
                   cr.ValorRecebido
            FROM ContaReceber cr JOIN Venda v ON v.Id=cr.VendaId
            JOIN Pedido p ON p.Id=v.PedidoId
            JOIN FormaPagamento fp ON fp.Id=cr.FormaPagamentoId
            WHERE p.PedidoGeralId=@Id ORDER BY v.PedidoId,cr.NumeroParcela
            """, new() { ["Id"] = id });
        SugerirCortes(itens);
        return new { lote = lotes[0], visitas, pedidos, itens, parcelas, contas };
    }

    private static async Task<List<Dictionary<string, object?>>> CarregarPedidos(
        MySqlConnection c, MySqlTransaction? t, int id) => await Consultar(c, t, """
        SELECT p.Id,p.ClienteId,cl.Nome AS Cliente,p.CondicaoPagamentoId,
               cp.Nome AS Condicao,cp.QuantidadeParcelas,cp.DescontoAVista,
               p.TotalPrevisto,p.TotalFinal,p.Situacao
        FROM Pedido p JOIN Cliente cl ON cl.Id=p.ClienteId
        JOIN CondicaoPagamento cp ON cp.Id=p.CondicaoPagamentoId
        WHERE p.PedidoGeralId=@Id ORDER BY p.Id
        """, new() { ["Id"] = id });

    private static async Task<List<Dictionary<string, object?>>> CarregarItens(
        MySqlConnection c, MySqlTransaction? t, int id) => await Consultar(c, t, """
        SELECT i.Id,i.PedidoId,i.ProdutoId,
               i.NomeProdutoSnapshot AS Produto,i.PrecoUnitarioSnapshot,
               i.QuantidadeSolicitada,i.QuantidadeAtendida,
               v.OrdemSnapshot AS OrdemVisita,cl.Nome AS Cliente,
               pr.Estoque,pr.DataValidade,pr.Ativo
        FROM ItemPedido i JOIN Pedido p ON p.Id=i.PedidoId
        JOIN Visita v ON v.Id=p.VisitaId
        JOIN Cliente cl ON cl.Id=p.ClienteId
        JOIN Produto pr ON pr.Id=i.ProdutoId
        WHERE p.PedidoGeralId=@Id
        ORDER BY i.ProdutoId,v.OrdemSnapshot,i.Id
        """, new() { ["Id"] = id });

    private static async Task<List<Dictionary<string, object?>>> CarregarParcelas(
        MySqlConnection c, MySqlTransaction? t, int id) => await Consultar(c, t, """
        SELECT pp.Id,pp.PedidoId,pp.Numero,pp.PrazoDias,
               pp.FormaPagamentoId,fp.Nome AS Forma,pp.ValorPrevisto
        FROM ParcelaPedido pp JOIN Pedido p ON p.Id=pp.PedidoId
        JOIN FormaPagamento fp ON fp.Id=pp.FormaPagamentoId
        WHERE p.PedidoGeralId=@Id ORDER BY pp.PedidoId,pp.Numero
        """, new() { ["Id"] = id });

    private static void SugerirCortes(List<Dictionary<string, object?>> itens)
    {
        var hojeUtc = DateTime.UtcNow.Date;
        foreach (var grupo in itens.GroupBy(x => Inteiro(x["ProdutoId"])))
        {
            var primeiro = grupo.First();
            var ativo = Convert.ToBoolean(primeiro["Ativo"], CultureInfo.InvariantCulture);
            var valido = Convert.ToDateTime(primeiro["DataValidade"], CultureInfo.InvariantCulture).Date >= hojeUtc;
            var estoque = ativo && valido ? Longo(primeiro["Estoque"]) : 0L;
            var total = grupo.Sum(x => Longo(x["QuantidadeSolicitada"]));
            var disponivel = Math.Min(estoque, total);
            var linhas = grupo.Select(x =>
            {
                var solicitada = Inteiro(x["QuantidadeSolicitada"]);
                var quota = total == 0 ? 0m : (decimal)disponivel * solicitada / total;
                var inteira = (long)decimal.Truncate(quota);
                return new
                {
                    Item = x, Inteira = inteira, Fracao = quota - inteira,
                    Ordem = Inteiro(x["OrdemVisita"]), Id = Inteiro(x["Id"])
                };
            }).ToList();
            foreach (var linha in linhas)
            {
                linha.Item["QuantidadeSugerida"] = linha.Inteira;
                linha.Item["DisponivelProduto"] = estoque;
                linha.Item["ProdutoVencido"] = !valido;
            }
            var sobras = disponivel - linhas.Sum(x => x.Inteira);
            foreach (var linha in linhas.OrderByDescending(x => x.Fracao)
                         .ThenBy(x => x.Ordem).ThenBy(x => x.Id).Take((int)sobras))
                linha.Item["QuantidadeSugerida"] = linha.Inteira + 1;
        }
    }

    private static async Task<List<Dictionary<string, object?>>> Consultar(
        MySqlConnection c, MySqlTransaction? t, string sql, Dictionary<string, object?>? args = null)
    {
        await using var cmd = new MySqlCommand(sql, c, t);
        Banco.AdicionarParametros(cmd, args);
        await using var leitor = await cmd.ExecuteReaderAsync();
        var resultado = new List<Dictionary<string, object?>>();
        while (await leitor.ReadAsync())
        {
            var linha = new Dictionary<string, object?>();
            for (var i = 0; i < leitor.FieldCount; i++)
                linha[leitor.GetName(i)] = leitor.IsDBNull(i) ? null : leitor.GetValue(i);
            resultado.Add(linha);
        }
        return resultado;
    }

    private static async Task<int> Executar(
        MySqlConnection c, MySqlTransaction t, string sql, Dictionary<string, object?> args)
    {
        await using var cmd = new MySqlCommand(sql, c, t);
        Banco.AdicionarParametros(cmd, args);
        return await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<long> Inserir(
        MySqlConnection c, MySqlTransaction t, string sql, Dictionary<string, object?> args)
    {
        await using var cmd = new MySqlCommand(sql, c, t);
        Banco.AdicionarParametros(cmd, args);
        await cmd.ExecuteNonQueryAsync();
        return cmd.LastInsertedId;
    }

    private static int Inteiro(object? valor) => Convert.ToInt32(valor, CultureInfo.InvariantCulture);
    private static long Longo(object? valor) => Convert.ToInt64(valor, CultureInfo.InvariantCulture);
    private static decimal Decimal(object? valor) => Convert.ToDecimal(valor, CultureInfo.InvariantCulture);
    private static string Texto(object? valor) => Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "";
    private static decimal Arredondar(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    private sealed class RegraCorteException(string mensagem) : Exception(mensagem)
    {
    }
}
