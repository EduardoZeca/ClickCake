using ClickCake.Api.Data;
using ClickCake.Api.Dtos;
using ClickCake.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickCake.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrador")]
[Route("api/admin")]
public class PessoasAdminController(Banco banco, IPasswordHasher<Usuario> hasher) : ControllerBase
{
    [HttpGet("vendedores")]
    public async Task<IActionResult> Vendedores() => Ok(await banco.ConsultarAsync(
        "SELECT Id,Nome,Login,Ativo FROM Usuario WHERE Perfil='Vendedor' ORDER BY Nome"));

    [HttpPost("vendedores")]
    public async Task<IActionResult> CriarVendedor(VendedorCadastroDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.Login) ||
            string.IsNullOrWhiteSpace(dto.Senha))
            return BadRequest(new { mensagem = "Nome, login e senha sao obrigatorios." });
        var usuario = new Usuario { Nome = dto.Nome.Trim(), Login = dto.Login.Trim(), Perfil = "Vendedor", Ativo = true };
        var hash = hasher.HashPassword(usuario, dto.Senha);
        try
        {
            await banco.ExecutarAsync("""
                INSERT INTO Usuario (Nome,Login,SenhaHash,Perfil,Ativo)
                VALUES (@Nome,@Login,@Hash,'Vendedor',TRUE)
                """, new Dictionary<string, object?> { ["Nome"] = usuario.Nome, ["Login"] = usuario.Login, ["Hash"] = hash });
            return Ok(new { mensagem = "Vendedor cadastrado." });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        { return Conflict(new { mensagem = "Login ja utilizado." }); }
    }

    [HttpPut("vendedores/{id:int}")]
    public async Task<IActionResult> AtualizarVendedor(int id, VendedorEdicaoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.Login))
            return BadRequest(new { mensagem = "Nome e login sao obrigatorios." });
        if (!dto.Ativo)
        {
            var clientes = await banco.ConsultarAsync(
                "SELECT COUNT(*) AS Total FROM Cliente WHERE VendedorId=@Id AND Ativo=TRUE",
                new Dictionary<string, object?> { ["Id"] = id });
            if (Convert.ToInt32(clientes[0]["Total"]) > 0)
                return Conflict(new { mensagem = "Vendedor ainda possui clientes ativos. Programe antes a retirada ou transferencia da carteira." });
        }
        var dados = new Dictionary<string, object?>
        {
            ["Id"] = id, ["Nome"] = dto.Nome.Trim(),
            ["Login"] = dto.Login.Trim(), ["Ativo"] = dto.Ativo
        };
        var sql = "UPDATE Usuario SET Nome=@Nome,Login=@Login,Ativo=@Ativo";
        if (!string.IsNullOrEmpty(dto.NovaSenha))
        {
            var usuario = new Usuario { Id = id, Nome = dto.Nome, Login = dto.Login, Perfil = "Vendedor" };
            dados["Hash"] = hasher.HashPassword(usuario, dto.NovaSenha);
            sql += ",SenhaHash=@Hash";
        }
        sql += " WHERE Id=@Id AND Perfil='Vendedor'";
        try
        {
            var linhas = await banco.ExecutarAsync(sql, dados);
            return linhas == 0 ? NotFound() : Ok(new { mensagem = "Vendedor atualizado." });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        { return Conflict(new { mensagem = "Login ja utilizado." }); }
    }

    [HttpDelete("vendedores/{id:int}")]
    public async Task<IActionResult> DesativarVendedor(int id)
    {
        var ativos = await banco.ConsultarAsync(
            "SELECT COUNT(*) AS Total FROM Cliente WHERE VendedorId=@Id AND Ativo=TRUE",
            new Dictionary<string, object?> { ["Id"] = id });
        if (Convert.ToInt32(ativos[0]["Total"]) > 0)
            return Conflict(new { mensagem = "O vendedor ainda possui clientes ativos. Programe a retirada ou transferencia antes de desativa-lo." });
        var linhas = await banco.ExecutarAsync(
            "UPDATE Usuario SET Ativo=FALSE WHERE Id=@Id AND Perfil='Vendedor'",
            new Dictionary<string, object?> { ["Id"] = id });
        return linhas == 0 ? NotFound() : NoContent();
    }

    [HttpGet("clientes")]
    public async Task<IActionResult> Clientes([FromQuery] int? vendedorId)
    {
        var sql = """
            SELECT c.Id,c.VendedorId,u.Nome AS Vendedor,c.CodigoVisita,c.OrdemVisita,
                c.Nome,c.Documento,c.Telefone,c.Email,c.Logradouro,c.Numero,
                c.Complemento,c.Bairro,c.Cep,c.CidadeId,
                ci.Nome AS Cidade,c.Ativo
            FROM Cliente c JOIN Usuario u ON u.Id=c.VendedorId
            JOIN Cidade ci ON ci.Id=c.CidadeId
            """;
        var parametros = new Dictionary<string, object?>();
        if (vendedorId.HasValue)
        {
            sql += " WHERE c.VendedorId=@VendedorId";
            parametros["VendedorId"] = vendedorId.Value;
        }
        return Ok(await banco.ConsultarAsync(sql + " ORDER BY u.Nome,c.OrdemVisita,c.Nome", parametros));
    }

    [HttpPost("clientes")]
    public async Task<IActionResult> CriarCliente(ClienteCadastroDto dto)
    {
        if (dto.VendedorId <= 0 || dto.CidadeId <= 0 || dto.CodigoVisita <= 0 ||
            string.IsNullOrWhiteSpace(dto.Nome))
            return BadRequest(new { mensagem = "Vendedor, codigo, nome e cidade sao obrigatorios." });

        // Novos clientes aguardam agendamento antes de participar de uma rota.
        var ordem = await banco.ConsultarAsync(
            "SELECT COALESCE(MAX(OrdemVisita),0)+1 AS Proxima FROM Cliente WHERE VendedorId=@VendedorId",
            new Dictionary<string, object?> { ["VendedorId"] = dto.VendedorId });
        var proxima = Convert.ToInt32(ordem[0]["Proxima"]);
        var parametros = ParametrosCliente(dto.Nome, dto.CidadeId, dto.Documento,
            dto.Telefone, dto.Email, dto.Logradouro, dto.Numero,
            dto.Complemento, dto.Bairro, dto.Cep);
        parametros["VendedorId"] = dto.VendedorId;
        parametros["CodigoVisita"] = dto.CodigoVisita;
        parametros["OrdemVisita"] = proxima;
        try
        {
            await banco.ExecutarAsync("""
                INSERT INTO Cliente (VendedorId,CodigoVisita,OrdemVisita,Nome,Documento,Telefone,
                    Email,Logradouro,Numero,Complemento,Bairro,Cep,CidadeId,Ativo)
                VALUES (@VendedorId,@CodigoVisita,@OrdemVisita,@Nome,@Documento,@Telefone,
                    @Email,@Logradouro,@Numero,@Complemento,@Bairro,@Cep,@CidadeId,FALSE)
                """, parametros);
            return Ok(new { mensagem = "Cliente cadastrado. Programe sua entrada na rota." });
        }
        catch (MySqlException ex) when (ex.Number is 1062 or 1452)
        { return Conflict(new { mensagem = "Codigo repetido, vendedor ou cidade inexistente." }); }
    }

    [HttpPut("clientes/{id:int}")]
    public async Task<IActionResult> AtualizarCliente(int id, ClienteEdicaoDto dto)
    {
        if (dto.CidadeId <= 0 || string.IsNullOrWhiteSpace(dto.Nome))
            return BadRequest(new { mensagem = "Nome e cidade sao obrigatorios." });
        var parametros = ParametrosCliente(dto.Nome, dto.CidadeId, dto.Documento,
            dto.Telefone, dto.Email, dto.Logradouro, dto.Numero,
            dto.Complemento, dto.Bairro, dto.Cep);
        parametros["Id"] = id;
        try
        {
            var linhas = await banco.ExecutarAsync("""
                UPDATE Cliente SET Nome=@Nome,Documento=@Documento,Telefone=@Telefone,
                    Email=@Email,Logradouro=@Logradouro,Numero=@Numero,
                    Complemento=@Complemento,Bairro=@Bairro,Cep=@Cep,CidadeId=@CidadeId
                WHERE Id=@Id
                """, parametros);
            return linhas == 0 ? NotFound() : Ok(new { mensagem = "Cliente atualizado." });
        }
        catch (MySqlException ex) when (ex.Number == 1452)
        { return Conflict(new { mensagem = "Cidade inexistente." }); }
    }

    [HttpDelete("clientes/{id:int}")]
    public IActionResult DesativarCliente(int id) => Conflict(new
    {
        mensagem = "A retirada do cliente deve ser agendada na rota com pelo menos 24 horas de antecedencia."
    });

    private static Dictionary<string, object?> ParametrosCliente(
        string nome, int cidadeId, string? documento, string? telefone, string? email,
        string? logradouro, string? numero, string? complemento, string? bairro, string? cep)
        => new()
        {
            ["Nome"] = nome.Trim(), ["CidadeId"] = cidadeId,
            ["Documento"] = documento, ["Telefone"] = telefone,
            ["Email"] = email, ["Logradouro"] = logradouro,
            ["Numero"] = numero, ["Complemento"] = complemento,
            ["Bairro"] = bairro, ["Cep"] = cep
        };
}
