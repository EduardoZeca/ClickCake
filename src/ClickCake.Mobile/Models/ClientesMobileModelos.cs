namespace ClickCake.Mobile.Models;

public sealed class CidadeCadastroDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Estado { get; set; } = "";
    public string Descricao => $"{Nome} / {Estado}";
}

public sealed class NovoClienteDto
{
    public string Nome { get; set; } = "";
    public int CidadeId { get; set; }
    public string? Documento { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cep { get; set; }
}

public sealed class NovoClienteResposta
{
    public long Id { get; set; }
    public string Nome { get; set; } = "";
    public int CodigoVisita { get; set; }
    public bool Ativo { get; set; }
    public string Mensagem { get; set; } = "";
}
