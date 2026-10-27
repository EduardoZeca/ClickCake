namespace ClickCake.Mobile.Models;

public class LoginResposta
{
    public string Token { get; set; } = "";
    public DateTime ExpiraEmUtc { get; set; }
    public UsuarioResposta Usuario { get; set; } = new();
}

public class UsuarioResposta
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Perfil { get; set; } = "";
}

public class SincronizacaoResposta
{
    public DateTime ServidorEmUtc { get; set; }
    public int? RotaVersaoId { get; set; }
    public DateTime? RotaVigenciaEmUtc { get; set; }
    public List<ClienteRotaDto> Clientes { get; set; } = new();
    public List<CategoriaDto> Categorias { get; set; } = new();
    public List<ProdutoDto> Produtos { get; set; } = new();
    public List<CondicaoDto> Condicoes { get; set; } = new();
    public List<FormaDto> Formas { get; set; } = new();
}

public class ClienteRotaDto
{
    public int Id { get; set; }
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
    public int CodigoVisita { get; set; }
    public int OrdemVisita { get; set; }
}

public class CategoriaDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
}

public class ProdutoDto
{
    public int Id { get; set; }
    public int CategoriaId { get; set; }
    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }
    public decimal Preco { get; set; }
    public DateTime DataValidade { get; set; }
}

public class CondicaoDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public int QuantidadeParcelas { get; set; }
    public string PrazosDias { get; set; } = "";
    public decimal DescontoAVista { get; set; }
}

public class FormaDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
}
