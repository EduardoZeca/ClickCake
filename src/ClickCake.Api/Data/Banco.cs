using MySqlConnector;

namespace ClickCake.Api.Data;

public sealed class Banco
{
    private readonly string _conexao;

    public Banco(IConfiguration configuracao)
    {
        _conexao = configuracao.GetConnectionString("ClickCake")
            ?? throw new InvalidOperationException("Conexao MySQL nao configurada.");
    }

    public MySqlConnection Abrir() => new(_conexao);

    public async Task<List<Dictionary<string, object?>>> ConsultarAsync(
        string sql, IReadOnlyDictionary<string, object?>? parametros = null)
    {
        await using var conexao = Abrir();
        await conexao.OpenAsync();
        await using var comando = new MySqlCommand(sql, conexao);
        AdicionarParametros(comando, parametros);
        await using var leitor = await comando.ExecuteReaderAsync();
        var registros = new List<Dictionary<string, object?>>();
        while (await leitor.ReadAsync())
        {
            var registro = new Dictionary<string, object?>();
            for (var i = 0; i < leitor.FieldCount; i++)
            {
                registro[leitor.GetName(i)] = leitor.IsDBNull(i)
                    ? null : leitor.GetValue(i);
            }
            registros.Add(registro);
        }
        return registros;
    }

    public async Task<int> ExecutarAsync(
        string sql, IReadOnlyDictionary<string, object?>? parametros = null)
    {
        await using var conexao = Abrir();
        await conexao.OpenAsync();
        await using var comando = new MySqlCommand(sql, conexao);
        AdicionarParametros(comando, parametros);
        return await comando.ExecuteNonQueryAsync();
    }

    public static void AdicionarParametros(
        MySqlCommand comando, IReadOnlyDictionary<string, object?>? parametros)
    {
        if (parametros == null) return;
        foreach (var (nome, valor) in parametros)
            comando.Parameters.AddWithValue($"@{nome}", valor ?? DBNull.Value);
    }
}
