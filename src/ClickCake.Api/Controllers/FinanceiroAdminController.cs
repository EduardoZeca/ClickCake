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
[Route("api/admin/financeiro")]
public sealed class FinanceiroAdminController(Banco banco) : ControllerBase
{
    [HttpGet("parcelas")]
    public async Task<IActionResult> Listar(
        [FromQuery] int? clienteId, [FromQuery] string? situacao,
        [FromQuery] DateTime? inicio, [FromQuery] DateTime? fim)
    {
        if (clienteId is <= 0 || (situacao is not (null or "" or "Aberta" or "Vencida" or "Paga")) ||
            (inicio.HasValue && fim.HasValue && inicio > fim))
            return BadRequest(new { mensagem = "Filtros invalidos." });

        var lista = await banco.ConsultarAsync("""
            SELECT cr.Id,cr.VendaId,v.PedidoId,p.PedidoGeralId,
                   c.Id AS ClienteId,c.Nome AS Cliente,u.Nome AS Vendedor,
                   cr.NumeroParcela,fp.Nome AS FormaPagamento,cr.Vencimento,
                   cr.ValorBase,cr.Situacao,
                   CASE WHEN cr.Situacao='Paga' THEN 'Paga'
                        WHEN cr.Vencimento<@Hoje THEN 'Vencida' ELSE 'Aberta' END AS SituacaoExibicao,
                   cr.DataRecebimento,cr.Multa,cr.Juros,cr.DescontoAntecipacao,cr.ValorRecebido
            FROM ContaReceber cr
            JOIN Venda v ON v.Id=cr.VendaId
            JOIN Pedido p ON p.Id=v.PedidoId
            JOIN Cliente c ON c.Id=p.ClienteId
            JOIN PedidoGeral g ON g.Id=p.PedidoGeralId
            JOIN Usuario u ON u.Id=g.VendedorId
            JOIN FormaPagamento fp ON fp.Id=cr.FormaPagamentoId
            WHERE (@ClienteId IS NULL OR c.Id=@ClienteId)
              AND (@Inicio IS NULL OR cr.Vencimento>=@Inicio)
              AND (@Fim IS NULL OR cr.Vencimento<=@Fim)
              AND (@Situacao IS NULL OR
                   (CASE WHEN cr.Situacao='Paga' THEN 'Paga'
                         WHEN cr.Vencimento<@Hoje THEN 'Vencida' ELSE 'Aberta' END)=@Situacao)
            ORDER BY cr.Vencimento,cr.Id
            """, new Dictionary<string, object?>
        {
            ["ClienteId"] = clienteId,
            ["Situacao"] = string.IsNullOrWhiteSpace(situacao) ? null : situacao,
            ["Inicio"] = inicio?.Date,
            ["Fim"] = fim?.Date,
            ["Hoje"] = DateTime.UtcNow.Date
        });
        return Ok(lista);
    }

    [HttpGet("parcelas/{id:int}/simular")]
    public async Task<IActionResult> Simular(int id, [FromQuery] DateTime data)
    {
        if (id <= 0 || data == default)
            return BadRequest(new { mensagem = "Informe identificador e data de pagamento." });
        var registros = await banco.ConsultarAsync(SqlParcela, new Dictionary<string, object?>
        {
            ["Id"] = id
        });
        if (registros.Count == 0) return NotFound(new { mensagem = "Parcela nao encontrada." });
        if (Texto(registros[0]["Situacao"]) != "Aberta")
            return Conflict(new { mensagem = "Esta parcela ja foi recebida." });
        var erro = ValidarData(registros[0], data.Date);
        if (erro != null) return BadRequest(new { mensagem = erro });
        return Ok(Calcular(registros[0], data.Date));
    }

    [HttpPost("parcelas/{id:int}/receber")]
    public async Task<IActionResult> Receber(int id, [FromBody] ReceberParcelaDto? dto)
    {
        if (id <= 0 || dto is null || dto.DataRecebimento == default)
            return BadRequest(new { mensagem = "Informe a data do recebimento." });

        await using var conexao = banco.Abrir();
        await conexao.OpenAsync();
        await using var transacao = await conexao.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await using var consulta = new MySqlCommand(SqlParcela + " FOR UPDATE", conexao, transacao);
        consulta.Parameters.AddWithValue("@Id", id);
        Dictionary<string, object?>? parcela = null;
        await using (var leitor = await consulta.ExecuteReaderAsync())
        {
            if (await leitor.ReadAsync())
            {
                parcela = new Dictionary<string, object?>();
                for (var i = 0; i < leitor.FieldCount; i++)
                    parcela[leitor.GetName(i)] = leitor.IsDBNull(i) ? null : leitor.GetValue(i);
            }
        }
        if (parcela == null) return NotFound(new { mensagem = "Parcela nao encontrada." });
        if (Texto(parcela["Situacao"]) != "Aberta")
            return Conflict(new { mensagem = "Esta parcela ja foi recebida." });
        var dataPagamento = dto.DataRecebimento.Date;
        var erro = ValidarData(parcela, dataPagamento);
        if (erro != null) return BadRequest(new { mensagem = erro });

        var calculo = Calcular(parcela, dataPagamento);
        await using var cmd = new MySqlCommand("""
            UPDATE ContaReceber
            SET Situacao='Paga', DataRecebimento=@Data,
                Multa=@Multa,Juros=@Juros,
                DescontoAntecipacao=@Desconto,ValorRecebido=@Valor
            WHERE Id=@Id AND Situacao='Aberta'
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Data", dataPagamento);
        cmd.Parameters.AddWithValue("@Multa", calculo.Multa);
        cmd.Parameters.AddWithValue("@Juros", calculo.Juros);
        cmd.Parameters.AddWithValue("@Desconto", calculo.DescontoAntecipacao);
        cmd.Parameters.AddWithValue("@Valor", calculo.ValorPago);
        if (await cmd.ExecuteNonQueryAsync() != 1)
            return Conflict(new { mensagem = "Parcela ja registrada por outra operacao." });
        await transacao.CommitAsync();
        return Ok(new
        {
            mensagem = "Recebimento integral registrado.",
            id, dataRecebimento = dataPagamento,
            calculo.ValorPago, calculo.Multa, calculo.Juros, calculo.DescontoAntecipacao
        });
    }

    private const string SqlParcela = """
        SELECT cr.Id,cr.Situacao,cr.ValorBase,cr.Vencimento,
               v.DataConfirmacao,c.Nome AS Cliente,cr.NumeroParcela
        FROM ContaReceber cr
        JOIN Venda v ON v.Id=cr.VendaId
        JOIN Pedido p ON p.Id=v.PedidoId
        JOIN Cliente c ON c.Id=p.ClienteId
        WHERE cr.Id=@Id
        """;

    private static string? ValidarData(Dictionary<string, object?> parcela, DateTime data)
    {
        var confirmacao = Convert.ToDateTime(parcela["DataConfirmacao"], CultureInfo.InvariantCulture).Date;
        if (data < confirmacao)
            return "Recebimento nao pode ocorrer antes da confirmacao da venda.";
        if (data > DateTime.UtcNow.Date)
            return "Nao e permitido registrar recebimento futuro.";
        return null;
    }

    private static PreviaRecebimento Calcular(Dictionary<string, object?> linha, DateTime data)
    {
        var valorBase = Convert.ToDecimal(linha["ValorBase"], CultureInfo.InvariantCulture);
        var vencimento = Convert.ToDateTime(linha["Vencimento"], CultureInfo.InvariantCulture).Date;
        var dias = (data - vencimento).Days;
        decimal multa = 0, juros = 0, desconto = 0;
        var pago = valorBase;
        if (dias > 0)
        {
            multa = Arredondar(valorBase * 0.05m);
            // Arredonda uma vez o total previsto pela formula.
            // O ultimo componente absorve o ajuste de centavos.
            pago = Arredondar(valorBase * (1.05m + 0.02m * dias));
            juros = pago - valorBase - multa;
        }
        else if (dias < 0)
        {
            pago = Arredondar(valorBase * (1m - 0.01m * -dias));
            desconto = valorBase - pago;
        }
        return new PreviaRecebimento(
            Convert.ToInt32(linha["Id"], CultureInfo.InvariantCulture),
            Texto(linha["Cliente"]),
            Convert.ToInt32(linha["NumeroParcela"], CultureInfo.InvariantCulture),
            valorBase, vencimento, data, dias,
            multa, juros, desconto, pago);
    }

    private static decimal Arredondar(decimal n) =>
        Math.Round(n, 2, MidpointRounding.AwayFromZero);
    private static string Texto(object? valor) => Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "";

    private sealed record PreviaRecebimento(
        int Id, string Cliente, int NumeroParcela,
        decimal ValorBase, DateTime Vencimento, DateTime DataRecebimento, int Dias,
        decimal Multa, decimal Juros, decimal DescontoAntecipacao, decimal ValorPago);
}
