using ClickCake.Api.Data;
using ClickCake.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickCake.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrador")]
[Route("api/admin/rotas")]
public class RotasAdminController(Banco banco) : ControllerBase
{
    [HttpGet("{vendedorId:int}")]
    public async Task<IActionResult> Consultar(int vendedorId)
    {
        await using var conexao = banco.Abrir();
        await conexao.OpenAsync();
        var agora = DateTime.UtcNow;
        var atual = await CarregarRotaEmAsync(conexao, null, vendedorId, agora);
        var futuras = await banco.ConsultarAsync("""
            SELECT Id,VigenciaEm,CriadaEm FROM RotaVersao
            WHERE VendedorId=@VendedorId AND VigenciaEm>@Agora
            ORDER BY VigenciaEm
            """, new Dictionary<string, object?> { ["VendedorId"] = vendedorId, ["Agora"] = agora });
        return Ok(new { Atual = atual, Futuras = futuras });
    }

    [HttpPost("{vendedorId:int}")]
    public async Task<IActionResult> Programar(int vendedorId, RotaProgramadaDto dto)
    {
        if (dto.Itens == null || vendedorId <= 0)
            return BadRequest(new { mensagem = "Informe vendedor e roteiro." });

        var vigencia = dto.VigenciaEmUtc.ToUniversalTime();
        if (vigencia < DateTime.UtcNow.AddHours(24))
            return BadRequest(new { mensagem = "A vigencia deve ter pelo menos 24 horas de antecedencia." });

        if (dto.Itens.Any(i => i.ClienteId <= 0 || i.CodigoVisita <= 0 || i.OrdemVisita <= 0) ||
            dto.Itens.Select(i => i.ClienteId).Distinct().Count() != dto.Itens.Count ||
            dto.Itens.Select(i => i.CodigoVisita).Distinct().Count() != dto.Itens.Count ||
            dto.Itens.Select(i => i.OrdemVisita).Distinct().Count() != dto.Itens.Count)
            return BadRequest(new { mensagem = "Clientes, codigos e ordens devem ser validos e exclusivos." });

        await using var conexao = banco.Abrir();
        await conexao.OpenAsync();
        await using var transacao = await conexao.BeginTransactionAsync();
        try
        {
            var vendedores = await ConsultarLinhasAsync(conexao, transacao,
                "SELECT Id FROM Usuario WHERE Perfil='Vendedor' AND Ativo=TRUE");
            var idsVendedores = vendedores.Select(v => Convert.ToInt32(v["Id"])).ToList();
            if (!idsVendedores.Contains(vendedorId))
                return BadRequest(new { mensagem = "Vendedor nao encontrado ou inativo." });

            if (dto.Itens.Count > 0)
            {
                var ids = dto.Itens.Select(i => i.ClienteId).ToArray();
                var nomes = ids.Select((_, i) => $"@c{i}").ToArray();
                var parametros = ids.Select((id, i) => new KeyValuePair<string, object?>($"c{i}", id))
                    .ToDictionary(x => x.Key, x => x.Value);
                var encontrados = await ConsultarLinhasAsync(conexao, transacao,
                    $"SELECT Id FROM Cliente WHERE Id IN ({string.Join(',', nomes)})", parametros);
                if (encontrados.Count != ids.Length)
                    return BadRequest(new { mensagem = "A rota contem cliente inexistente." });
            }

            // Nenhum cliente pode integrar duas rotas vigentes no mesmo instante.
            var novosIds = dto.Itens.Select(i => i.ClienteId).ToHashSet();
            foreach (var outroVendedorId in idsVendedores.Where(id => id != vendedorId))
            {
                var outraRota = await CarregarRotaEmAsync(conexao, transacao, outroVendedorId, vigencia);
                if (outraRota.Itens.Any(item => novosIds.Contains(item.ClienteId)))
                {
                    return Conflict(new
                    {
                        mensagem = "Cliente ainda pertence a outra rota nessa data. Programe primeiro sua retirada da carteira de origem."
                    });
                }
            }

            await using var cabecalho = new MySqlCommand("""
                INSERT INTO RotaVersao (VendedorId,CriadaEm,VigenciaEm)
                VALUES (@VendedorId,@CriadaEm,@VigenciaEm)
                """, conexao, transacao);
            cabecalho.Parameters.AddWithValue("@VendedorId", vendedorId);
            cabecalho.Parameters.AddWithValue("@CriadaEm", DateTime.UtcNow);
            cabecalho.Parameters.AddWithValue("@VigenciaEm", vigencia);
            await cabecalho.ExecuteNonQueryAsync();
            var versaoId = cabecalho.LastInsertedId;

            foreach (var item in dto.Itens)
            {
                await using var comando = new MySqlCommand("""
                    INSERT INTO RotaVersaoItem (RotaVersaoId,ClienteId,CodigoVisita,OrdemVisita)
                    VALUES (@RotaVersaoId,@ClienteId,@CodigoVisita,@OrdemVisita)
                    """, conexao, transacao);
                comando.Parameters.AddWithValue("@RotaVersaoId", versaoId);
                comando.Parameters.AddWithValue("@ClienteId", item.ClienteId);
                comando.Parameters.AddWithValue("@CodigoVisita", item.CodigoVisita);
                comando.Parameters.AddWithValue("@OrdemVisita", item.OrdemVisita);
                await comando.ExecuteNonQueryAsync();
            }

            await transacao.CommitAsync();
            return Ok(new { mensagem = "Rota programada.", versaoId, vigenciaEmUtc = vigencia });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            await transacao.RollbackAsync();
            return Conflict(new { mensagem = "Ja existe programacao para este vendedor e horario." });
        }
    }

    private static async Task<RotaResultado> CarregarRotaEmAsync(
        MySqlConnection conexao, MySqlTransaction? transacao, int vendedorId, DateTime dataUtc)
    {
        var versoes = await ConsultarLinhasAsync(conexao, transacao, """
            SELECT Id,VigenciaEm FROM RotaVersao
            WHERE VendedorId=@VendedorId AND VigenciaEm<=@DataUtc
            ORDER BY VigenciaEm DESC LIMIT 1
            """, new Dictionary<string, object?> { ["VendedorId"] = vendedorId, ["DataUtc"] = dataUtc });
        if (versoes.Count > 0)
        {
            var id = Convert.ToInt32(versoes[0]["Id"]);
            var itens = await ConsultarLinhasAsync(conexao, transacao, """
                SELECT r.ClienteId,c.Nome,r.CodigoVisita,r.OrdemVisita
                FROM RotaVersaoItem r JOIN Cliente c ON c.Id=r.ClienteId
                WHERE r.RotaVersaoId=@RotaVersaoId ORDER BY r.OrdemVisita
                """, new Dictionary<string, object?> { ["RotaVersaoId"] = id });
            return new RotaResultado(id, (DateTime)versoes[0]["VigenciaEm"]!, Converter(itens));
        }

        var atuais = await ConsultarLinhasAsync(conexao, transacao, """
            SELECT Id AS ClienteId,Nome,CodigoVisita,OrdemVisita
            FROM Cliente WHERE VendedorId=@VendedorId AND Ativo=TRUE
            ORDER BY OrdemVisita
            """, new Dictionary<string, object?> { ["VendedorId"] = vendedorId });
        return new RotaResultado(null, null, Converter(atuais));
    }

    private static List<RotaCliente> Converter(List<Dictionary<string, object?>> linhas) =>
        linhas.Select(l => new RotaCliente(
            Convert.ToInt32(l["ClienteId"]),
            Convert.ToString(l["Nome"]) ?? "",
            Convert.ToInt32(l["CodigoVisita"]),
            Convert.ToInt32(l["OrdemVisita"]))).ToList();

    private static async Task<List<Dictionary<string, object?>>> ConsultarLinhasAsync(
        MySqlConnection conexao, MySqlTransaction? transacao, string sql,
        IReadOnlyDictionary<string, object?>? parametros = null)
    {
        await using var comando = new MySqlCommand(sql, conexao, transacao);
        Banco.AdicionarParametros(comando, parametros);
        await using var leitor = await comando.ExecuteReaderAsync();
        var dados = new List<Dictionary<string, object?>>();
        while (await leitor.ReadAsync())
        {
            var linha = new Dictionary<string, object?>();
            for (var i = 0; i < leitor.FieldCount; i++)
                linha[leitor.GetName(i)] = leitor.IsDBNull(i) ? null : leitor.GetValue(i);
            dados.Add(linha);
        }
        return dados;
    }

    private record RotaCliente(int ClienteId, string Nome, int CodigoVisita, int OrdemVisita);
    private record RotaResultado(int? VersaoId, DateTime? VigenciaEmUtc, List<RotaCliente> Itens);
}
