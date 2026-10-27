using SQLite;

namespace ClickCake.Mobile.Models;

[Table("Cliente")]
public class ClienteLocal
{
    [PrimaryKey] public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string? Documento { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cep { get; set; }
    public int CidadeId { get; set; }
}

[Table("RotaAtual")]
public class RotaItemLocal
{
    [PrimaryKey] public int ClienteId { get; set; }
    public int CodigoVisita { get; set; }
    public int OrdemVisita { get; set; }
}

[Table("Categoria")]
public class CategoriaLocal
{
    [PrimaryKey] public int Id { get; set; }
    public string Nome { get; set; } = "";
}

[Table("Produto")]
public class ProdutoLocal
{
    [PrimaryKey] public int Id { get; set; }
    public int CategoriaId { get; set; }
    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }
    public decimal Preco { get; set; }
    public DateTime DataValidade { get; set; }
}

[Table("CondicaoPagamento")]
public class CondicaoLocal
{
    [PrimaryKey] public int Id { get; set; }
    public string Nome { get; set; } = "";
    public int QuantidadeParcelas { get; set; }
    public string PrazosDias { get; set; } = "";
    public decimal DescontoAVista { get; set; }
}

[Table("FormaPagamento")]
public class FormaLocal
{
    [PrimaryKey] public int Id { get; set; }
    public string Nome { get; set; } = "";
}

[Table("Configuracao")]
public class ConfiguracaoLocal
{
    [PrimaryKey] public string Chave { get; set; } = "";
    public string Valor { get; set; } = "";
}

public class ClienteExibicao
{
    public string Nome { get; set; } = "";
    public int CodigoVisita { get; set; }
    public int OrdemVisita { get; set; }
    public string? Telefone { get; set; }

    public string Resumo =>
        $"{OrdemVisita}. {Nome}  |  Codigo: {CodigoVisita}" +
        (string.IsNullOrWhiteSpace(Telefone) ? "" : $"  |  {Telefone}");
}

public class ProdutoExibicao
{
    public string Nome { get; set; } = "";
    public decimal Preco { get; set; }
    public DateTime DataValidade { get; set; }

    public string Resumo => $"{Nome}  |  R$ {Preco:N2}  |  Validade: {DataValidade:dd/MM/yyyy}";
}

public class CondicaoExibicao
{
    public string Nome { get; set; } = "";
    public string PrazosDias { get; set; } = "";
    public int QuantidadeParcelas { get; set; }

    public string Resumo => $"{Nome}  |  {QuantidadeParcelas} parcela(s)  |  {PrazosDias}";
}
