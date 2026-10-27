using ClickCake.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickCake.Api.Controllers;

[ApiController]
[Authorize(Roles = "Vendedor")]
[Route("api/mobile")]
public class SincronizacaoMobileController(Banco banco) : ControllerBase
{
    [HttpGet("sincronizacao")]
    public async Task<IActionResult> Sincronizar()
    {
        if (!int.TryParse(User.FindFirst("id")?.Value, out var vendedorId))
            return Unauthorized(new { mensagem = "Sessao de vendedor invalida." });

        var agoraUtc = DateTime.UtcNow;
        var parametros = new Dictionary<string, object?>
        {
            ["VendedorId"] = vendedorId,
            ["AgoraUtc"] = agoraUtc
        };

        var versoes = await banco.ConsultarAsync("""
            SELECT Id, VigenciaEm FROM RotaVersao
            WHERE VendedorId=@VendedorId AND VigenciaEm<=@AgoraUtc
            ORDER BY VigenciaEm DESC LIMIT 1
            """, parametros);

        int? versaoId = null;
        DateTime? vigencia = null;
        List<Dictionary<string, object?>> clientes;

        const string camposCliente = """
            c.Id,c.Nome,c.Documento,c.Telefone,c.Email,c.Logradouro,c.Numero,
            c.Complemento,c.Bairro,c.Cep,c.CidadeId
            """;

        if (versoes.Count > 0)
        {
            versaoId = Convert.ToInt32(versoes[0]["Id"]);
            vigencia = (DateTime)versoes[0]["VigenciaEm"]!;
            clientes = await banco.ConsultarAsync($"""
                SELECT {camposCliente},r.CodigoVisita,r.OrdemVisita
                FROM RotaVersaoItem r
                JOIN Cliente c ON c.Id=r.ClienteId
                WHERE r.RotaVersaoId=@VersaoId
                ORDER BY r.OrdemVisita
                """, new Dictionary<string, object?> { ["VersaoId"] = versaoId });
        }
        else
        {
            clientes = await banco.ConsultarAsync($"""
                SELECT {camposCliente},c.CodigoVisita,c.OrdemVisita
                FROM Cliente c
                WHERE c.VendedorId=@VendedorId AND c.Ativo=TRUE
                ORDER BY c.OrdemVisita
                """, parametros);
        }

        var categorias = await banco.ConsultarAsync("""
            SELECT Id,Nome FROM Categoria WHERE Ativo=TRUE ORDER BY Nome
            """);
        // O estoque nunca e enviado para o aplicativo do vendedor.
        var produtos = await banco.ConsultarAsync("""
            SELECT p.Id,p.CategoriaId,p.Nome,p.Descricao,p.Preco,p.DataValidade
            FROM Produto p JOIN Categoria c ON c.Id=p.CategoriaId
            WHERE p.Ativo=TRUE AND c.Ativo=TRUE AND p.DataValidade>=CURRENT_DATE()
            ORDER BY c.Nome,p.Nome
            """);
        var condicoes = await banco.ConsultarAsync("""
            SELECT Id,Nome,QuantidadeParcelas,PrazosDias,DescontoAVista
            FROM CondicaoPagamento ORDER BY Id
            """);
        var formas = await banco.ConsultarAsync("""
            SELECT Id,Nome FROM FormaPagamento WHERE Ativo=TRUE ORDER BY Id
            """);

        return Ok(new
        {
            servidorEmUtc = DateTime.UtcNow,
            rotaVersaoId = versaoId,
            rotaVigenciaEmUtc = vigencia,
            clientes,
            categorias,
            produtos,
            condicoes,
            formas
        });
    }
}
