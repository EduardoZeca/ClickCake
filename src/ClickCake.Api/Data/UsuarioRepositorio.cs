using ClickCake.Api.Models;
using MySqlConnector;

namespace ClickCake.Api.Data;

public class UsuarioRepositorio
{
    private readonly string _connectionString;

    public UsuarioRepositorio(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("ClickCake")
            ?? throw new InvalidOperationException(
                "Conexao MySQL nao configurada.");
    }

    public async Task<Usuario?> BuscarPorLoginAsync(string login)
    {
        const string sql = """
            SELECT Id, Nome, Login, SenhaHash, Perfil, Ativo
            FROM Usuario
            WHERE Login = @Login
            LIMIT 1;
            """;

        await using var conexao =
            new MySqlConnection(_connectionString);

        await conexao.OpenAsync();

        await using var comando =
            new MySqlCommand(sql, conexao);

        comando.Parameters.AddWithValue("@Login", login);

        await using var leitor =
            await comando.ExecuteReaderAsync();

        if (!await leitor.ReadAsync())
            return null;

        return LerUsuario(leitor);
    }

    public async Task<Usuario?> BuscarPorIdAsync(int id)
    {
        const string sql = """
            SELECT Id, Nome, Login, SenhaHash, Perfil, Ativo
            FROM Usuario
            WHERE Id = @Id
            LIMIT 1;
            """;

        await using var conexao =
            new MySqlConnection(_connectionString);

        await conexao.OpenAsync();

        await using var comando =
            new MySqlCommand(sql, conexao);

        comando.Parameters.AddWithValue("@Id", id);

        await using var leitor =
            await comando.ExecuteReaderAsync();

        if (!await leitor.ReadAsync())
            return null;

        return LerUsuario(leitor);
    }

    public async Task<bool> ExisteAdministradorAsync()
    {
        const string sql = """
            SELECT COUNT(*)
            FROM Usuario
            WHERE Perfil = 'Administrador';
            """;

        await using var conexao =
            new MySqlConnection(_connectionString);

        await conexao.OpenAsync();

        await using var comando =
            new MySqlCommand(sql, conexao);

        var total = Convert.ToInt64(
            await comando.ExecuteScalarAsync());

        return total > 0;
    }

    public async Task InserirAdministradorAsync(Usuario usuario)
    {
        const string sql = """
            INSERT INTO Usuario
                (Nome, Login, SenhaHash, Perfil, Ativo)
            VALUES
                (@Nome, @Login, @SenhaHash, 'Administrador', TRUE);
            """;

        await using var conexao =
            new MySqlConnection(_connectionString);

        await conexao.OpenAsync();

        await using var comando =
            new MySqlCommand(sql, conexao);

        comando.Parameters.AddWithValue("@Nome", usuario.Nome);
        comando.Parameters.AddWithValue("@Login", usuario.Login);
        comando.Parameters.AddWithValue(
            "@SenhaHash", usuario.SenhaHash);

        await comando.ExecuteNonQueryAsync();
    }

    private static Usuario LerUsuario(MySqlDataReader leitor)
    {
        return new Usuario
        {
            Id = leitor.GetInt32(0),
            Nome = leitor.GetString(1),
            Login = leitor.GetString(2),
            SenhaHash = leitor.GetString(3),
            Perfil = leitor.GetString(4),
            Ativo = leitor.GetBoolean(5)
        };
    }
}