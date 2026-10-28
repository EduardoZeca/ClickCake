namespace ClickCake.Api.Dtos;

public sealed class ItemCorteDto
{
    public int ItemPedidoId { get; set; }
    public int QuantidadeAtendida { get; set; }
}

public sealed class ConfirmacaoCorteDto
{
    public List<ItemCorteDto> Itens { get; set; } = [];
}

public sealed class CancelamentoLoteDto
{
    public string Motivo { get; set; } = string.Empty;
}
