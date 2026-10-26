using ClickCake.Api.Data;
using ClickCake.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickCake.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrador")]
[Route("api/admin")]
public class CatalogoAdminController(Banco banco) : ControllerBase
{
    [HttpGet("categorias")]
    public async Task<IActionResult> Categorias() => Ok(await banco.ConsultarAsync(
        "SELECT Id, Nome, Ativo FROM Categoria ORDER BY Nome"));

    [HttpPost("categorias")]
    public async Task<IActionResult> CriarCategoria(CategoriaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome)) return BadRequest(new { mensagem = "Nome obrigatorio." });
        try
        {
            await banco.ExecutarAsync("INSERT INTO Categoria (Nome,Ativo) VALUES (@Nome,@Ativo)",
                new Dictionary<string, object?> { ["Nome"] = dto.Nome.Trim(), ["Ativo"] = dto.Ativo });
            return Ok(new { mensagem = "Categoria cadastrada." });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        { return Conflict(new { mensagem = "Categoria ja cadastrada." }); }
    }

    [HttpPut("categorias/{id:int}")]
    public async Task<IActionResult> AtualizarCategoria(int id, CategoriaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome)) return BadRequest(new { mensagem = "Nome obrigatorio." });
        try
        {
            var linhas = await banco.ExecutarAsync("UPDATE Categoria SET Nome=@Nome,Ativo=@Ativo WHERE Id=@Id",
                new Dictionary<string, object?> { ["Id"] = id, ["Nome"] = dto.Nome.Trim(), ["Ativo"] = dto.Ativo });
            return linhas == 0 ? NotFound() : Ok(new { mensagem = "Categoria atualizada." });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        { return Conflict(new { mensagem = "Categoria ja cadastrada." }); }
    }

    [HttpDelete("categorias/{id:int}")]
    public async Task<IActionResult> DesativarCategoria(int id)
    {
        var linhas = await banco.ExecutarAsync("UPDATE Categoria SET Ativo=FALSE WHERE Id=@Id",
            new Dictionary<string, object?> { ["Id"] = id });
        return linhas == 0 ? NotFound() : NoContent();
    }

    [HttpGet("produtos")]
    public async Task<IActionResult> Produtos([FromQuery] string? validade, [FromQuery] int dias = 7)
    {
        const string baseSql = """
            SELECT p.Id,p.CategoriaId,c.Nome AS Categoria,p.Nome,p.Descricao,
                   p.Preco,p.Estoque,p.DataValidade,p.Ativo
            FROM Produto p JOIN Categoria c ON c.Id=p.CategoriaId
            """;
        var hoje = DateTime.Today;
        var parametros = new Dictionary<string, object?> { ["Hoje"] = hoje, ["Dias"] = dias };
        var filtro = validade?.ToLowerInvariant() switch
        {
            "vencidos" => " WHERE p.DataValidade < @Hoje",
            "proximos" when dias is >= 0 and <= 365 =>
                " WHERE p.DataValidade >= @Hoje AND p.DataValidade <= DATE_ADD(@Hoje, INTERVAL @Dias DAY)",
            null or "" or "todos" => "",
            _ => null
        };
        if (filtro == null) return BadRequest(new { mensagem = "Filtro de validade invalido." });
        return Ok(await banco.ConsultarAsync(baseSql + filtro + " ORDER BY p.DataValidade,p.Nome", parametros));
    }

    [HttpPost("produtos")]
    public async Task<IActionResult> CriarProduto(ProdutoDto dto)
    {
        var erro = ValidarProduto(dto);
        if (erro != null) return BadRequest(new { mensagem = erro });
        try
        {
            await banco.ExecutarAsync("""
                INSERT INTO Produto (CategoriaId,Nome,Descricao,Preco,Estoque,DataValidade,Ativo)
                VALUES (@CategoriaId,@Nome,@Descricao,@Preco,@Estoque,@DataValidade,@Ativo)
                """, ParametrosProduto(dto));
            return Ok(new { mensagem = "Produto cadastrado." });
        }
        catch (MySqlException ex) when (ex.Number == 1452)
        { return Conflict(new { mensagem = "Categoria inexistente." }); }
    }

    [HttpPut("produtos/{id:int}")]
    public async Task<IActionResult> AtualizarProduto(int id, ProdutoDto dto)
    {
        var erro = ValidarProduto(dto);
        if (erro != null) return BadRequest(new { mensagem = erro });
        var parametros = ParametrosProduto(dto);
        parametros["Id"] = id;
        try
        {
            var linhas = await banco.ExecutarAsync("""
                UPDATE Produto SET CategoriaId=@CategoriaId,Nome=@Nome,Descricao=@Descricao,
                    Preco=@Preco,Estoque=@Estoque,DataValidade=@DataValidade,Ativo=@Ativo
                WHERE Id=@Id
                """, parametros);
            return linhas == 0 ? NotFound() : Ok(new { mensagem = "Produto atualizado." });
        }
        catch (MySqlException ex) when (ex.Number == 1452)
        { return Conflict(new { mensagem = "Categoria inexistente." }); }
    }

    [HttpDelete("produtos/{id:int}")]
    public async Task<IActionResult> DesativarProduto(int id)
    {
        var linhas = await banco.ExecutarAsync("UPDATE Produto SET Ativo=FALSE WHERE Id=@Id",
            new Dictionary<string, object?> { ["Id"] = id });
        return linhas == 0 ? NotFound() : NoContent();
    }

    [HttpGet("formas")]
    public async Task<IActionResult> Formas() => Ok(await banco.ConsultarAsync(
        "SELECT Id,Nome,Ativo FROM FormaPagamento ORDER BY Id"));

    [HttpPost("formas")]
    public async Task<IActionResult> CriarForma(FormaPagamentoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome)) return BadRequest(new { mensagem = "Nome obrigatorio." });
        try
        {
            await banco.ExecutarAsync("INSERT INTO FormaPagamento (Nome,Ativo) VALUES (@Nome,@Ativo)",
                new Dictionary<string, object?> { ["Nome"] = dto.Nome.Trim(), ["Ativo"] = dto.Ativo });
            return Ok(new { mensagem = "Forma cadastrada." });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        { return Conflict(new { mensagem = "Forma ja cadastrada." }); }
    }

    [HttpPut("formas/{id:int}")]
    public async Task<IActionResult> AtualizarForma(int id, FormaPagamentoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome)) return BadRequest(new { mensagem = "Nome obrigatorio." });
        try
        {
            var linhas = await banco.ExecutarAsync("UPDATE FormaPagamento SET Nome=@Nome,Ativo=@Ativo WHERE Id=@Id",
                new Dictionary<string, object?> { ["Id"] = id, ["Nome"] = dto.Nome.Trim(), ["Ativo"] = dto.Ativo });
            return linhas == 0 ? NotFound() : Ok(new { mensagem = "Forma atualizada." });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        { return Conflict(new { mensagem = "Forma ja cadastrada." }); }
    }

    [HttpDelete("formas/{id:int}")]
    public async Task<IActionResult> DesativarForma(int id)
    {
        var linhas = await banco.ExecutarAsync("UPDATE FormaPagamento SET Ativo=FALSE WHERE Id=@Id",
            new Dictionary<string, object?> { ["Id"] = id });
        return linhas == 0 ? NotFound() : NoContent();
    }

    [HttpGet("condicoes")]
    public async Task<IActionResult> Condicoes() => Ok(await banco.ConsultarAsync(
        "SELECT Id,Nome,QuantidadeParcelas,PrazosDias,DescontoAVista FROM CondicaoPagamento ORDER BY Id"));

    private static string? ValidarProduto(ProdutoDto dto)
    {
        if (dto.CategoriaId <= 0 || string.IsNullOrWhiteSpace(dto.Nome))
            return "Informe categoria e nome.";
        if (dto.Preco < 0 || dto.Estoque < 0 || dto.DataValidade == default)
            return "Preco, estoque ou validade invalidos.";
        return null;
    }

    private static Dictionary<string, object?> ParametrosProduto(ProdutoDto dto) => new()
    {
        ["CategoriaId"] = dto.CategoriaId,
        ["Nome"] = dto.Nome.Trim(),
        ["Descricao"] = dto.Descricao,
        ["Preco"] = dto.Preco,
        ["Estoque"] = dto.Estoque,
        ["DataValidade"] = dto.DataValidade.Date,
        ["Ativo"] = dto.Ativo
    };
}
