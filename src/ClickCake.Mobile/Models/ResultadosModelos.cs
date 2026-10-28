using SQLite;

namespace ClickCake.Mobile.Models;

public sealed class ResultadosResposta
{
    public DateTime ServidorEmUtc { get; set; }
    public List<ResultadoLote> Lotes { get; set; } = [];
    public List<ResultadoPedido> Pedidos { get; set; } = [];
    public List<ResultadoItem> Itens { get; set; } = [];
    public List<ResultadoParcela> Parcelas { get; set; } = [];
}

public sealed class ResultadoLote
{
    public long Id { get; set; }
    public string IdentificadorEnvio { get; set; } = "";
    public string Situacao { get; set; } = "";
    public DateTime DataEnvio { get; set; }
    public DateTime? DataConfirmacao { get; set; }
    public string? MotivoCancelamento { get; set; }
}

public sealed class ResultadoPedido
{
    public long Id { get; set; }
    public long PedidoGeralId { get; set; }
    public int ClienteId { get; set; }
    public string Cliente { get; set; } = "";
    public string Situacao { get; set; } = "";
    public string Condicao { get; set; } = "";
    public decimal TotalPrevisto { get; set; }
    public decimal? TotalFinal { get; set; }
}

public sealed class ResultadoItem
{
    public long PedidoId { get; set; }
    public int ProdutoId { get; set; }
    public string Produto { get; set; } = "";
    public int QuantidadeSolicitada { get; set; }
    public int? QuantidadeAtendida { get; set; }
    public int? QuantidadeCortada { get; set; }
    public decimal PrecoUnitarioSnapshot { get; set; }
}

public sealed class ResultadoParcela
{
    public long PedidoId { get; set; }
    public int Numero { get; set; }
    public int PrazoDias { get; set; }
    public string FormaPagamento { get; set; } = "";
    public decimal ValorPrevisto { get; set; }
    public DateTime? Vencimento { get; set; }
    public decimal? ValorBase { get; set; }
    public string? Situacao { get; set; }
    public DateTime? DataRecebimento { get; set; }
    public decimal? ValorRecebido { get; set; }
}

[Table("ResultadosCache")]
public sealed class ResultadosCacheLocal
{
    [PrimaryKey] public string Chave { get; set; } = "Atual";
    public string Json { get; set; } = "";
}
