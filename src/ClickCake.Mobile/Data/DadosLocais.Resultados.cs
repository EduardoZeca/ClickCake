using System.Text.Json;
using ClickCake.Mobile.Models;

namespace ClickCake.Mobile.Data;

public sealed partial class DadosLocais
{
    private static readonly JsonSerializerOptions JsonResultados = new(JsonSerializerDefaults.Web);

    public Task InicializarResultadosAsync() =>
        _banco.CreateTableAsync<ResultadosCacheLocal>();

    public async Task<ResultadosResposta?> LerResultadosAsync()
    {
        var registro = await _banco.FindAsync<ResultadosCacheLocal>("Atual");
        if (registro == null) return null;
        return JsonSerializer.Deserialize<ResultadosResposta>(registro.Json, JsonResultados);
    }

    public async Task SalvarResultadosAsync(ResultadosResposta resultado)
    {
        await _banco.InsertOrReplaceAsync(new ResultadosCacheLocal
        {
            Chave = "Atual",
            Json = JsonSerializer.Serialize(resultado, JsonResultados)
        });
    }
}
