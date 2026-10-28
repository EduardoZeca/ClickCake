using ClickCake.Api.Data;
using ClickCake.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickCake.Api.Controllers;

[ApiController]
[Authorize(Roles = "Vendedor")]
[Route("api/mobile/clientes")]
public sealed class ClientesMobileController(Banco banco) : ControllerBase
{
    [HttpGet("cidades")]
    public async Task<IActionResult> Cidades()
    {
        if (!int.TryParse(User.FindFirst("id")?.Value, out var vendedorId) || vendedorId <= 0)
            return Unauthorized();

        return Ok(await banco.ConsultarAsync("""
            SELECT c.Id,c.Nome,c.EstadoId,e.Sigla AS Estado
            FROM Cidade c JOIN Estado e ON e.Id=c.EstadoId
            ORDER BY e.Sigla,c.Nome
            """));
    }

    [HttpPost]
    public async Task<IActionResult> Cadastrar(ClienteMobileDto dto)
    {
        if (!int.TryParse(User.FindFirst("id")?.Value, out var vendedorId) || vendedorId <= 0)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Nome) || dto.Nome.Trim().Length > 150 || dto.CidadeId <= 0)
            return BadRequest(new { mensagem = "Informe nome (ate 150 caracteres) e cidade." });

        if (dto.Documento?.Length > 20 || dto.Telefone?.Length > 20 ||
            dto.Email?.Length > 150 || dto.Logradouro?.Length > 200 ||
            dto.Numero?.Length > 20 || dto.Complemento?.Length > 100 ||
            dto.Bairro?.Length > 100 || dto.Cep?.Length > 10)
            return BadRequest(new { mensagem = "Algum campo excede o tamanho permitido." });

        // A identidade e carteira sao determinadas exclusivamente pelo JWT.
        // A trava no usuario serializa cadastros simultaneos do mesmo vendedor.
        await using var conexao = banco.Abrir();
        await conexao.OpenAsync();
        await using var transacao = await conexao.BeginTransactionAsync();
        try
        {
            await using (var trava = new MySqlCommand("""
                SELECT Id FROM Usuario
                WHERE Id=@VendedorId AND Perfil='Vendedor' AND Ativo=TRUE
                FOR UPDATE
                """, conexao, transacao))
            {
                trava.Parameters.AddWithValue("@VendedorId", vendedorId);
                if (await trava.ExecuteScalarAsync() == null)
                    return Unauthorized();
            }

            int codigo, ordem;
            await using (var proximos = new MySqlCommand("""
                SELECT COALESCE(MAX(CodigoVisita),0)+1 AS Codigo,
                       COALESCE(MAX(OrdemVisita),0)+1 AS Ordem
                FROM Cliente WHERE VendedorId=@VendedorId
                """, conexao, transacao))
            {
                proximos.Parameters.AddWithValue("@VendedorId", vendedorId);
                await using var leitor = await proximos.ExecuteReaderAsync();
                await leitor.ReadAsync();
                codigo = Convert.ToInt32(leitor["Codigo"]);
                ordem = Convert.ToInt32(leitor["Ordem"]);
            }

            long id;
            await using (var inserir = new MySqlCommand("""
                INSERT INTO Cliente
                    (VendedorId,CodigoVisita,OrdemVisita,Nome,Documento,Telefone,Email,
                     Logradouro,Numero,Complemento,Bairro,Cep,CidadeId,Ativo)
                VALUES
                    (@VendedorId,@Codigo,@Ordem,@Nome,@Documento,@Telefone,@Email,
                     @Logradouro,@Numero,@Complemento,@Bairro,@Cep,@CidadeId,FALSE)
                """, conexao, transacao))
            {
                inserir.Parameters.AddWithValue("@VendedorId", vendedorId);
                inserir.Parameters.AddWithValue("@Codigo", codigo);
                inserir.Parameters.AddWithValue("@Ordem", ordem);
                inserir.Parameters.AddWithValue("@Nome", dto.Nome.Trim());
                inserir.Parameters.AddWithValue("@CidadeId", dto.CidadeId);
                inserir.Parameters.AddWithValue("@Documento", ValorOuDbNull(dto.Documento));
                inserir.Parameters.AddWithValue("@Telefone", ValorOuDbNull(dto.Telefone));
                inserir.Parameters.AddWithValue("@Email", ValorOuDbNull(dto.Email));
                inserir.Parameters.AddWithValue("@Logradouro", ValorOuDbNull(dto.Logradouro));
                inserir.Parameters.AddWithValue("@Numero", ValorOuDbNull(dto.Numero));
                inserir.Parameters.AddWithValue("@Complemento", ValorOuDbNull(dto.Complemento));
                inserir.Parameters.AddWithValue("@Bairro", ValorOuDbNull(dto.Bairro));
                inserir.Parameters.AddWithValue("@Cep", ValorOuDbNull(dto.Cep));
                await inserir.ExecuteNonQueryAsync();
                id = inserir.LastInsertedId;
            }

            await transacao.CommitAsync();
            return Ok(new
            {
                id,
                nome = dto.Nome.Trim(),
                codigoVisita = codigo,
                ativo = false,
                mensagem = "Cliente cadastrado. Aguarde o administrador programar a entrada na rota."
            });
        }
        catch (MySqlException ex) when (ex.Number is 1062 or 1452)
        {
            await transacao.RollbackAsync();
            return Conflict(new { mensagem = "Cidade inexistente ou cadastro concorrente. Confira e tente novamente." });
        }
    }

    private static object ValorOuDbNull(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim();
}
