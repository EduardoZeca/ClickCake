using ClickCake.Api.Data;
using ClickCake.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickCake.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrador")]
[Route("api/admin")]
public class LocalidadesController(Banco banco) : ControllerBase
{
    [HttpGet("estados")]
    public async Task<IActionResult> Estados() => Ok(await banco.ConsultarAsync(
        "SELECT Id, Nome, Sigla FROM Estado ORDER BY Nome"));

    [HttpPost("estados")]
    public async Task<IActionResult> CriarEstado(EstadoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome) || dto.Sigla?.Trim().Length != 2)
            return BadRequest(new { mensagem = "Informe nome e sigla de duas letras." });
        try
        {
            await banco.ExecutarAsync("INSERT INTO Estado (Nome, Sigla) VALUES (@Nome, @Sigla)",
                new Dictionary<string, object?> { ["Nome"] = dto.Nome.Trim(), ["Sigla"] = dto.Sigla.Trim().ToUpperInvariant() });
            return Ok(new { mensagem = "Estado cadastrado." });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        { return Conflict(new { mensagem = "Sigla ja cadastrada." }); }
    }

    [HttpPut("estados/{id:int}")]
    public async Task<IActionResult> AtualizarEstado(int id, EstadoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome) || dto.Sigla?.Trim().Length != 2)
            return BadRequest(new { mensagem = "Informe nome e sigla de duas letras." });
        try
        {
            var linhas = await banco.ExecutarAsync(
                "UPDATE Estado SET Nome=@Nome, Sigla=@Sigla WHERE Id=@Id",
                new Dictionary<string, object?> { ["Id"] = id, ["Nome"] = dto.Nome.Trim(), ["Sigla"] = dto.Sigla.Trim().ToUpperInvariant() });
            return linhas == 0 ? NotFound() : Ok(new { mensagem = "Estado atualizado." });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        { return Conflict(new { mensagem = "Sigla ja cadastrada." }); }
    }

    [HttpDelete("estados/{id:int}")]
    public async Task<IActionResult> RemoverEstado(int id)
    {
        try
        {
            var linhas = await banco.ExecutarAsync("DELETE FROM Estado WHERE Id=@Id",
                new Dictionary<string, object?> { ["Id"] = id });
            return linhas == 0 ? NotFound() : NoContent();
        }
        catch (MySqlException ex) when (ex.Number == 1451)
        { return Conflict(new { mensagem = "Estado possui cidades vinculadas." }); }
    }

    [HttpGet("cidades")]
    public async Task<IActionResult> Cidades() => Ok(await banco.ConsultarAsync(
        "SELECT c.Id, c.Nome, c.EstadoId, e.Sigla AS Estado FROM Cidade c JOIN Estado e ON e.Id=c.EstadoId ORDER BY e.Sigla,c.Nome"));

    [HttpPost("cidades")]
    public async Task<IActionResult> CriarCidade(CidadeDto dto)
    {
        if (dto.EstadoId <= 0 || string.IsNullOrWhiteSpace(dto.Nome))
            return BadRequest(new { mensagem = "Informe estado e nome." });
        try
        {
            await banco.ExecutarAsync("INSERT INTO Cidade (EstadoId, Nome) VALUES (@EstadoId,@Nome)",
                new Dictionary<string, object?> { ["EstadoId"] = dto.EstadoId, ["Nome"] = dto.Nome.Trim() });
            return Ok(new { mensagem = "Cidade cadastrada." });
        }
        catch (MySqlException ex) when (ex.Number is 1062 or 1452)
        { return Conflict(new { mensagem = "Cidade duplicada ou estado inexistente." }); }
    }

    [HttpPut("cidades/{id:int}")]
    public async Task<IActionResult> AtualizarCidade(int id, CidadeDto dto)
    {
        if (dto.EstadoId <= 0 || string.IsNullOrWhiteSpace(dto.Nome))
            return BadRequest(new { mensagem = "Informe estado e nome." });
        try
        {
            var linhas = await banco.ExecutarAsync("UPDATE Cidade SET EstadoId=@EstadoId, Nome=@Nome WHERE Id=@Id",
                new Dictionary<string, object?> { ["Id"] = id, ["EstadoId"] = dto.EstadoId, ["Nome"] = dto.Nome.Trim() });
            return linhas == 0 ? NotFound() : Ok(new { mensagem = "Cidade atualizada." });
        }
        catch (MySqlException ex) when (ex.Number is 1062 or 1452)
        { return Conflict(new { mensagem = "Cidade duplicada ou estado inexistente." }); }
    }

    [HttpDelete("cidades/{id:int}")]
    public async Task<IActionResult> RemoverCidade(int id)
    {
        try
        {
            var linhas = await banco.ExecutarAsync("DELETE FROM Cidade WHERE Id=@Id",
                new Dictionary<string, object?> { ["Id"] = id });
            return linhas == 0 ? NotFound() : NoContent();
        }
        catch (MySqlException ex) when (ex.Number == 1451)
        { return Conflict(new { mensagem = "Cidade possui clientes vinculados." }); }
    }
}
