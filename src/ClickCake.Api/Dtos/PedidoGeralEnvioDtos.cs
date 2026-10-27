namespace ClickCake.Api.Dtos;

public sealed class PedidoGeralEnvioDto
{
    public Guid IdentificadorEnvio { get; set; }
    public DateTime DataCriacaoUtc { get; set; }
    public int? RotaVersaoId { get; set; }
    public List<VisitaEnvioDto> Visitas { get; set; } = new();
}

public sealed class VisitaEnvioDto
{
    public int ClienteId { get; set; }
    public int CodigoVisita { get; set; }
    public int OrdemVisita { get; set; }
    public string Situacao { get; set; } = "";
    public string? MotivoPulo { get; set; }
    public string? Observacao { get; set; }
    public PedidoEnvioDto? Pedido { get; set; }
}

public sealed class PedidoEnvioDto
{
    public int CondicaoPagamentoId { get; set; }
    public decimal TotalPrevisto { get; set; }
    public List<ItemPedidoEnvioDto> Itens { get; set; } = new();
    public List<ParcelaEnvioDto> Parcelas { get; set; } = new();
}

public sealed class ItemPedidoEnvioDto
{
    public int ProdutoId { get; set; }
    public string NomeProduto { get; set; } = "";
    public decimal PrecoUnitario { get; set; }
    public int Quantidade { get; set; }
}

public sealed class ParcelaEnvioDto
{
    public int Numero { get; set; }
    public int PrazoDias { get; set; }
    public int FormaPagamentoId { get; set; }
    public decimal ValorPrevisto { get; set; }
}
