using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClickCake.Api.Data;
using ClickCake.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickCake.Api.Controllers;

[ApiController]
[Authorize(Roles = "Vendedor")]
[Route("api/mobile/pedidosgerais")]
public sealed class PedidosMobileController(Banco banco) : ControllerBase
{
    [HttpGet("{identificador:guid}")]
    public async Task<IActionResult> Consultar(Guid identificador)
    {
        if (!ObterVendedor(out var vendedorId)) return Unauthorized();
        var linhas = await banco.ConsultarAsync("""
            SELECT Id, Situacao FROM PedidoGeral
            WHERE IdentificadorEnvio=@Identificador AND VendedorId=@VendedorId
            """, new Dictionary<string, object?>
        {
            ["Identificador"] = identificador.ToString(), ["VendedorId"] = vendedorId
        });
        if (linhas.Count == 0) return NotFound(new { mensagem = "Envio nao localizado." });
        return Ok(new
        {
            pedidoGeralId = Convert.ToInt64(linhas[0]["Id"]),
            identificadorEnvio = identificador,
            situacao = linhas[0]["Situacao"],
            repetido = true
        });
    }

    [HttpPost]
    public async Task<IActionResult> Receber([FromBody] PedidoGeralEnvioDto dto)
    {
        if (!ObterVendedor(out var vendedorId)) return Unauthorized();
        if (dto is null || dto.IdentificadorEnvio == Guid.Empty)
            return BadRequest(new { mensagem = "Identificador de envio invalido." });
        if (dto.Visitas is null || dto.Visitas.Any(v => v is null ||
            (v.Pedido != null && (v.Pedido.Itens == null || v.Pedido.Parcelas == null))))
            return BadRequest(new { mensagem = "Visitas, itens ou parcelas invalidos." });

        // O hash considera o conteudo completo recebido. Nao muda em tentativas de rede.
        var hash = Hash(dto);
        await using var conexao = banco.Abrir();
        await conexao.OpenAsync();
        await using var transacao = await conexao.BeginTransactionAsync();
        try
        {
            var anterior = await BuscarEnvioAsync(conexao, transacao, dto.IdentificadorEnvio);
            if (anterior != null)
            {
                await transacao.CommitAsync();
                return RespostaAnterior(anterior, vendedorId, hash, dto.IdentificadorEnvio);
            }

            await ValidarAsync(conexao, transacao, dto, vendedorId);
            var idLote = await InserirAsync(conexao, transacao, """
                INSERT INTO PedidoGeral
                  (VendedorId,IdentificadorEnvio,HashConteudo,DataCriacao,DataEnvio,Situacao)
                VALUES (@VendedorId,@IdentificadorEnvio,@HashConteudo,@DataCriacao,@DataEnvio,'Recebido')
                """, new()
            {
                ["VendedorId"] = vendedorId,
                ["IdentificadorEnvio"] = dto.IdentificadorEnvio.ToString(),
                ["HashConteudo"] = hash,
                ["DataCriacao"] = dto.DataCriacaoUtc.ToUniversalTime(),
                ["DataEnvio"] = DateTime.UtcNow
            });

            foreach (var visita in dto.Visitas.OrderBy(v => v.OrdemVisita))
            {
                var idVisita = await InserirAsync(conexao, transacao, """
                    INSERT INTO Visita
                      (PedidoGeralId,ClienteId,OrdemSnapshot,CodigoVisitaSnapshot,
                       Situacao,MotivoPulo,Observacao)
                    VALUES (@Lote,@Cliente,@Ordem,@Codigo,@Situacao,@Motivo,@Observacao)
                    """, new()
                {
                    ["Lote"] = idLote, ["Cliente"] = visita.ClienteId,
                    ["Ordem"] = visita.OrdemVisita, ["Codigo"] = visita.CodigoVisita,
                    ["Situacao"] = visita.Situacao, ["Motivo"] = visita.MotivoPulo,
                    ["Observacao"] = visita.Observacao?.Trim()
                });

                if (visita.Pedido is not { } pedido) continue;
                var idPedido = await InserirAsync(conexao, transacao, """
                    INSERT INTO Pedido
                      (PedidoGeralId,ClienteId,VisitaId,CondicaoPagamentoId,Situacao,TotalPrevisto)
                    VALUES (@Lote,@Cliente,@Visita,@Condicao,'Recebido',@Total)
                    """, new()
                {
                    ["Lote"] = idLote, ["Cliente"] = visita.ClienteId,
                    ["Visita"] = idVisita, ["Condicao"] = pedido.CondicaoPagamentoId,
                    ["Total"] = pedido.TotalPrevisto
                });

                foreach (var item in pedido.Itens)
                    await InserirAsync(conexao, transacao, """
                        INSERT INTO ItemPedido
                          (PedidoId,ProdutoId,NomeProdutoSnapshot,PrecoUnitarioSnapshot,QuantidadeSolicitada)
                        VALUES (@Pedido,@Produto,@Nome,@Preco,@Quantidade)
                        """, new()
                    {
                        ["Pedido"] = idPedido, ["Produto"] = item.ProdutoId,
                        ["Nome"] = item.NomeProduto, ["Preco"] = item.PrecoUnitario,
                        ["Quantidade"] = item.Quantidade
                    });

                foreach (var parcela in pedido.Parcelas.OrderBy(p => p.Numero))
                    await InserirAsync(conexao, transacao, """
                        INSERT INTO ParcelaPedido
                          (PedidoId,Numero,PrazoDias,FormaPagamentoId,ValorPrevisto)
                        VALUES (@Pedido,@Numero,@Prazo,@Forma,@Valor)
                        """, new()
                    {
                        ["Pedido"] = idPedido, ["Numero"] = parcela.Numero,
                        ["Prazo"] = parcela.PrazoDias, ["Forma"] = parcela.FormaPagamentoId,
                        ["Valor"] = parcela.ValorPrevisto
                    });
            }

            await transacao.CommitAsync();
            return Ok(new
            {
                pedidoGeralId = idLote,
                identificadorEnvio = dto.IdentificadorEnvio,
                situacao = "Recebido", repetido = false
            });
        }
        catch (RegraEnvioException ex)
        {
            await transacao.RollbackAsync();
            return BadRequest(new { mensagem = ex.Message });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            // Duas tentativas simultaneas com o mesmo identificador: apenas uma grava.
            await transacao.RollbackAsync();
            var existente = await BuscarEnvioAsync(conexao, null, dto.IdentificadorEnvio);
            if (existente != null)
                return RespostaAnterior(existente, vendedorId, hash, dto.IdentificadorEnvio);
            return Conflict(new { mensagem = "Conflito de integridade. Consulte o identificador antes de reenviar." });
        }
    }

    private bool ObterVendedor(out int vendedorId) =>
        int.TryParse(User.FindFirst("id")?.Value, out vendedorId) && vendedorId > 0;

    private IActionResult RespostaAnterior(
        Dictionary<string, object?> existente, int vendedorId, string hash, Guid identificador)
    {
        if (Convert.ToInt32(existente["VendedorId"]) != vendedorId)
            return Conflict(new { mensagem = "Identificador ja utilizado." });
        if (!string.Equals(Convert.ToString(existente["HashConteudo"]), hash,
                StringComparison.OrdinalIgnoreCase))
            return Conflict(new { mensagem = "Identificador reutilizado com conteudo diferente." });
        return Ok(new
        {
            pedidoGeralId = Convert.ToInt64(existente["Id"]),
            identificadorEnvio = identificador,
            situacao = existente["Situacao"], repetido = true
        });
    }

    private static string Hash(PedidoGeralEnvioDto dto)
    {
        // Nao inclui o GUID: a identidade e a chave unica; o hash verifica o conteudo.
        var conteudo = new
        {
            dto.DataCriacaoUtc, dto.RotaVersaoId,
            visitas = dto.Visitas.OrderBy(v => v.OrdemVisita).Select(v => new
            {
                v.ClienteId, v.CodigoVisita, v.OrdemVisita, v.Situacao, v.MotivoPulo,
                v.Observacao,
                pedido = v.Pedido == null ? null : new
                {
                    v.Pedido.CondicaoPagamentoId, v.Pedido.TotalPrevisto,
                    itens = v.Pedido.Itens.OrderBy(i => i.ProdutoId),
                    parcelas = v.Pedido.Parcelas.OrderBy(p => p.Numero)
                }
            })
        };
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(conteudo)))).ToLowerInvariant();
    }

    private static async Task ValidarAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        PedidoGeralEnvioDto dto, int vendedorId)
    {
        var inicio = dto.DataCriacaoUtc.ToUniversalTime();
        if (dto.DataCriacaoUtc == default || inicio > DateTime.UtcNow.AddMinutes(5))
            throw new RegraEnvioException("Data de inicio do ciclo invalida.");
        if (dto.Visitas is not { Count: > 0 } ||
            dto.Visitas.Count > 1000 ||
            dto.Visitas.Select(v => v.ClienteId).Distinct().Count() != dto.Visitas.Count ||
            dto.Visitas.Select(v => v.OrdemVisita).Distinct().Count() != dto.Visitas.Count)
            throw new RegraEnvioException("Roteiro vazio, duplicado ou invalido.");

        var versao = await ConsultarAsync(conexao, transacao, """
            SELECT Id FROM RotaVersao
            WHERE VendedorId=@Vendedor AND VigenciaEm<=@Inicio
            ORDER BY VigenciaEm DESC LIMIT 1
            """, new() { ["Vendedor"] = vendedorId, ["Inicio"] = inicio });
        var versaoEsperada = versao.Count == 0 ? (int?)null : Convert.ToInt32(versao[0]["Id"]);
        if (dto.RotaVersaoId != versaoEsperada)
            throw new RegraEnvioException("Versao da rota nao corresponde ao inicio do ciclo.");

        var rota = dto.RotaVersaoId is int versaoId
            ? await ConsultarAsync(conexao, transacao, """
                SELECT ClienteId,CodigoVisita,OrdemVisita FROM RotaVersaoItem
                WHERE RotaVersaoId=@Versao ORDER BY OrdemVisita
                """, new() { ["Versao"] = versaoId })
            : await ConsultarAsync(conexao, transacao, """
                SELECT Id AS ClienteId,CodigoVisita,OrdemVisita FROM Cliente
                WHERE VendedorId=@Vendedor AND Ativo=TRUE ORDER BY OrdemVisita
                """, new() { ["Vendedor"] = vendedorId });
        var visitasOrdenadas = dto.Visitas.OrderBy(v => v.OrdemVisita).ToList();
        if (rota.Count != visitasOrdenadas.Count)
            throw new RegraEnvioException("A rota enviada nao contem todos os clientes do ciclo.");

        var totalPedidos = 0;
        for (var index = 0; index < rota.Count; index++)
        {
            var visita = visitasOrdenadas[index];
            var original = rota[index];
            if (visita.ClienteId != Convert.ToInt32(original["ClienteId"]) ||
                visita.CodigoVisita != Convert.ToInt32(original["CodigoVisita"]) ||
                visita.OrdemVisita != Convert.ToInt32(original["OrdemVisita"]))
                throw new RegraEnvioException($"Rota divergente na visita {index + 1}.");

            if (visita.Situacao == "Pulado")
            {
                if (visita.Pedido != null ||
                    visita.MotivoPulo is not ("NaoFaraPedido" or "LocalFechado" or "Indisponivel" or "Outro") ||
                    (visita.MotivoPulo == "Outro" && string.IsNullOrWhiteSpace(visita.Observacao)) ||
                    visita.Observacao?.Length > 500)
                    throw new RegraEnvioException($"Motivo invalido na visita de cliente {visita.ClienteId}.");
            }
            else if (visita.Situacao == "PedidoRegistrado" && visita.Pedido != null &&
                     visita.MotivoPulo == null)
            {
                totalPedidos++;
                await ValidarPedidoAsync(conexao, transacao, visita.ClienteId, visita.Pedido);
            }
            else
                throw new RegraEnvioException($"Visita incompleta do cliente {visita.ClienteId}.");
        }
        if (totalPedidos == 0)
            throw new RegraEnvioException("Nao e permitido enviar lote sem pedidos.");
    }

    private static async Task ValidarPedidoAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        int clienteId, PedidoEnvioDto pedido)
    {
        if (pedido.Itens is not { Count: > 0 } || pedido.Itens.Count > 500 ||
            pedido.Itens.Select(i => i.ProdutoId).Distinct().Count() != pedido.Itens.Count ||
            pedido.Parcelas is not { Count: > 0 })
            throw new RegraEnvioException($"Itens ou parcelas invalidos do cliente {clienteId}.");

        var condicoes = await ConsultarAsync(conexao, transacao, """
            SELECT QuantidadeParcelas,PrazosDias,DescontoAVista FROM CondicaoPagamento
            WHERE Id=@Id
            """, new() { ["Id"] = pedido.CondicaoPagamentoId });
        if (condicoes.Count != 1)
            throw new RegraEnvioException($"Condicao invalida do cliente {clienteId}.");
        var condicao = condicoes[0];
        var prazos = (Convert.ToString(condicao["PrazosDias"]) ?? "")
            .Split(',', StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();
        var qtdParcelas = Convert.ToInt32(condicao["QuantidadeParcelas"]);
        if (prazos.Length != qtdParcelas || pedido.Parcelas.Count != qtdParcelas ||
            !pedido.Parcelas.Select(p => p.Numero).Order().SequenceEqual(Enumerable.Range(1, qtdParcelas)))
            throw new RegraEnvioException($"Parcelamento incompleto do cliente {clienteId}.");

        decimal subtotal = 0;
        foreach (var item in pedido.Itens)
        {
            if (item.Quantidade <= 0 || item.ProdutoId <= 0 || item.PrecoUnitario < 0)
                throw new RegraEnvioException($"Quantidade ou preco invalido: cliente {clienteId}, produto {item.ProdutoId}.");
            var produtos = await ConsultarAsync(conexao, transacao, """
                SELECT p.Nome,p.Preco FROM Produto p JOIN Categoria c ON c.Id=p.CategoriaId
                WHERE p.Id=@Id AND p.Ativo=TRUE AND c.Ativo=TRUE
                """, new() { ["Id"] = item.ProdutoId });
            if (produtos.Count != 1 ||
                Convert.ToDecimal(produtos[0]["Preco"], CultureInfo.InvariantCulture) != item.PrecoUnitario ||
                !string.Equals(Convert.ToString(produtos[0]["Nome"]), item.NomeProduto, StringComparison.Ordinal))
                throw new RegraEnvioException($"Produto ou preco divergente: cliente {clienteId}, produto {item.ProdutoId}. Sincronize e revise.");
            subtotal += item.Quantidade * item.PrecoUnitario;
            if (subtotal > 9999999999m)
                throw new RegraEnvioException("Valor do pedido excede o limite permitido.");
        }
        var desconto = Convert.ToDecimal(condicao["DescontoAVista"], CultureInfo.InvariantCulture);
        var total = Arredondar(subtotal * (1 - desconto));
        if (pedido.TotalPrevisto != total)
            throw new RegraEnvioException($"Total divergente do cliente {clienteId}.");

        decimal acumulado = 0;
        for (var index = 0; index < qtdParcelas; index++)
        {
            var parcela = pedido.Parcelas.Single(p => p.Numero == index + 1);
            var esperado = index == qtdParcelas - 1
                ? total - acumulado : Arredondar(total / qtdParcelas);
            acumulado += esperado;
            if (parcela.PrazoDias != prazos[index] || parcela.ValorPrevisto != esperado)
                throw new RegraEnvioException($"Parcela divergente do cliente {clienteId}.");
            var formas = await ConsultarAsync(conexao, transacao, """
                SELECT Id FROM FormaPagamento WHERE Id=@Id AND Ativo=TRUE
                """, new() { ["Id"] = parcela.FormaPagamentoId });
            if (formas.Count != 1)
                throw new RegraEnvioException($"Forma de pagamento invalida do cliente {clienteId}.");
        }
    }

    private static decimal Arredondar(decimal valor) =>
        Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    private static async Task<Dictionary<string, object?>?> BuscarEnvioAsync(
        MySqlConnection conexao, MySqlTransaction? transacao, Guid identificador)
    {
        var linhas = await ConsultarAsync(conexao, transacao, """
            SELECT Id,VendedorId,HashConteudo,Situacao FROM PedidoGeral
            WHERE IdentificadorEnvio=@Identificador LIMIT 1
            """, new() { ["Identificador"] = identificador.ToString() });
        return linhas.FirstOrDefault();
    }

    private static async Task<List<Dictionary<string, object?>>> ConsultarAsync(
        MySqlConnection conexao, MySqlTransaction? transacao,
        string sql, Dictionary<string, object?> parametros)
    {
        await using var comando = new MySqlCommand(sql, conexao, transacao);
        Banco.AdicionarParametros(comando, parametros);
        await using var leitor = await comando.ExecuteReaderAsync();
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

    private static async Task<long> InserirAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        string sql, Dictionary<string, object?> parametros)
    {
        await using var comando = new MySqlCommand(sql, conexao, transacao);
        Banco.AdicionarParametros(comando, parametros);
        await comando.ExecuteNonQueryAsync();
        return comando.LastInsertedId;
    }

    private sealed class RegraEnvioException(string mensagem) : Exception(mensagem);
}
