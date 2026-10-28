using ClickCake.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickCake.Api.Controllers;

[ApiController]
[Authorize(Roles = "Vendedor")]
[Route("api/mobile/resultados")]
public sealed class ResultadosMobileController(Banco banco) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Consultar()
    {
        if (!int.TryParse(User.FindFirst("id")?.Value, out var vendedorId) || vendedorId <= 0)
            return Unauthorized();

        var filtro = new Dictionary<string, object?> { ["VendedorId"] = vendedorId };
        var lotes = await banco.ConsultarAsync("""
            SELECT g.Id,g.IdentificadorEnvio,g.Situacao,
                   g.DataEnvio,g.DataConfirmacao,g.MotivoCancelamento
            FROM PedidoGeral g
            WHERE g.VendedorId=@VendedorId
            ORDER BY g.DataEnvio DESC,g.Id DESC
            """, filtro);

        var pedidos = await banco.ConsultarAsync("""
            SELECT p.Id,p.PedidoGeralId,p.ClienteId,c.Nome AS Cliente,
                   p.Situacao,p.TotalPrevisto,p.TotalFinal,cp.Nome AS Condicao
            FROM Pedido p
            JOIN PedidoGeral g ON g.Id=p.PedidoGeralId
            JOIN Cliente c ON c.Id=p.ClienteId
            JOIN CondicaoPagamento cp ON cp.Id=p.CondicaoPagamentoId
            WHERE g.VendedorId=@VendedorId
            ORDER BY p.PedidoGeralId DESC,p.Id
            """, filtro);

        var itens = await banco.ConsultarAsync("""
            SELECT i.PedidoId,i.ProdutoId,i.NomeProdutoSnapshot AS Produto,
                   i.QuantidadeSolicitada,i.QuantidadeAtendida,
                   CASE WHEN i.QuantidadeAtendida IS NULL THEN NULL
                        ELSE i.QuantidadeSolicitada-i.QuantidadeAtendida END AS QuantidadeCortada,
                   i.PrecoUnitarioSnapshot
            FROM ItemPedido i
            JOIN Pedido p ON p.Id=i.PedidoId
            JOIN PedidoGeral g ON g.Id=p.PedidoGeralId
            WHERE g.VendedorId=@VendedorId
            ORDER BY i.PedidoId,i.Id
            """, filtro);

        var parcelas = await banco.ConsultarAsync("""
            SELECT pp.PedidoId,pp.Numero,pp.PrazoDias,fp.Nome AS FormaPagamento,
                   pp.ValorPrevisto,cr.Vencimento,cr.ValorBase,
                   CASE WHEN cr.Situacao='Paga' THEN 'Paga'
                        WHEN cr.Situacao='Aberta' AND cr.Vencimento<@Hoje THEN 'Vencida'
                        ELSE cr.Situacao END AS Situacao,
                   cr.DataRecebimento,cr.ValorRecebido
            FROM ParcelaPedido pp
            JOIN Pedido p ON p.Id=pp.PedidoId
            JOIN PedidoGeral g ON g.Id=p.PedidoGeralId
            JOIN FormaPagamento fp ON fp.Id=pp.FormaPagamentoId
            LEFT JOIN ContaReceber cr ON cr.ParcelaPedidoId=pp.Id
            WHERE g.VendedorId=@VendedorId
            ORDER BY pp.PedidoId,pp.Numero
            """, new Dictionary<string, object?>
        {
            ["VendedorId"] = vendedorId, ["Hoje"] = DateTime.UtcNow.Date
        });

        return Ok(new { servidorEmUtc = DateTime.UtcNow, lotes, pedidos, itens, parcelas });
    }
}
