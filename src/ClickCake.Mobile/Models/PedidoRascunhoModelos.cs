using SQLite;

namespace ClickCake.Mobile.Models;

[Table("PedidoGeralRascunho")]
public sealed class PedidoGeralRascunho
{
    [PrimaryKey] public string Id { get; set; } = "";
    public string IdentificadorEnvio { get; set; } = "";
    public string CriadoEmUtc { get; set; } = "";
    public int? RotaVersaoId { get; set; }
    public string Situacao { get; set; } = "EmAndamento";
    public long? PedidoGeralServidorId { get; set; }
}

[Table("VisitaRascunho")]
public sealed class VisitaRascunho
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string PedidoGeralId { get; set; } = "";
    public int ClienteId { get; set; }
    public string ClienteNome { get; set; } = "";
    public int CodigoVisita { get; set; }
    public int OrdemVisita { get; set; }
    public string Situacao { get; set; } = "Pendente";
    public string? MotivoPulo { get; set; }
    public string? Observacao { get; set; }
}

[Table("PedidoRascunho")]
public sealed class PedidoRascunho
{
    [PrimaryKey] public string Id { get; set; } = "";
    [Indexed] public string PedidoGeralId { get; set; } = "";
    [Indexed] public int VisitaId { get; set; }
    public int CondicaoPagamentoId { get; set; }
    public decimal TotalPrevisto { get; set; }
}

[Table("ItemPedidoRascunho")]
public sealed class ItemPedidoRascunho
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string PedidoId { get; set; } = "";
    public int ProdutoId { get; set; }
    public string NomeProduto { get; set; } = "";
    public decimal PrecoUnitario { get; set; }
    public int Quantidade { get; set; }
}

[Table("ParcelaRascunho")]
public sealed class ParcelaRascunho
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string PedidoId { get; set; } = "";
    public int Numero { get; set; }
    public int PrazoDias { get; set; }
    public int FormaPagamentoId { get; set; }
    public decimal ValorPrevisto { get; set; }
}

public sealed class ItemCarrinho
{
    public int ProdutoId { get; set; }
    public string NomeProduto { get; set; } = "";
    public decimal PrecoUnitario { get; set; }
    public int Quantidade { get; set; }
    public decimal Subtotal => Quantidade * PrecoUnitario;
    public string Resumo => $"{NomeProduto} x{Quantidade} = R$ {Subtotal:N2}";
}

public sealed class ParcelaEscolhida
{
    public int FormaPagamentoId { get; set; }
}

public sealed class PedidoGeralEnvio
{
    public Guid IdentificadorEnvio { get; set; }
    public DateTime DataCriacaoUtc { get; set; }
    public int? RotaVersaoId { get; set; }
    public List<VisitaEnvio> Visitas { get; set; } = new();
}

public sealed class VisitaEnvio
{
    public int ClienteId { get; set; }
    public int CodigoVisita { get; set; }
    public int OrdemVisita { get; set; }
    public string Situacao { get; set; } = "";
    public string? MotivoPulo { get; set; }
    public string? Observacao { get; set; }
    public PedidoEnvio? Pedido { get; set; }
}

public sealed class PedidoEnvio
{
    public int CondicaoPagamentoId { get; set; }
    public decimal TotalPrevisto { get; set; }
    public List<ItemEnvio> Itens { get; set; } = new();
    public List<ParcelaEnvio> Parcelas { get; set; } = new();
}

public sealed class ItemEnvio
{
    public int ProdutoId { get; set; }
    public string NomeProduto { get; set; } = "";
    public decimal PrecoUnitario { get; set; }
    public int Quantidade { get; set; }
}

public sealed class ParcelaEnvio
{
    public int Numero { get; set; }
    public int PrazoDias { get; set; }
    public int FormaPagamentoId { get; set; }
    public decimal ValorPrevisto { get; set; }
}

public sealed class ResultadoEnvio
{
    public long PedidoGeralId { get; set; }
    public Guid IdentificadorEnvio { get; set; }
    public string Situacao { get; set; } = "";
    public bool Repetido { get; set; }
}
