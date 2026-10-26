namespace ClickCake.Api.Dtos;

public record EstadoDto(string Nome, string Sigla);
public record CidadeDto(int EstadoId, string Nome);
public record CategoriaDto(string Nome, bool Ativo);
public record FormaPagamentoDto(string Nome, bool Ativo);
public record ProdutoDto(int CategoriaId, string Nome, string? Descricao,
    decimal Preco, int Estoque, DateTime DataValidade, bool Ativo);
public record VendedorCadastroDto(string Nome, string Login, string Senha);
public record VendedorEdicaoDto(string Nome, string Login, bool Ativo, string? NovaSenha);
public record ClienteCadastroDto(int VendedorId, int CodigoVisita, string Nome,
    int CidadeId, string? Documento, string? Telefone, string? Email,
    string? Logradouro, string? Numero, string? Complemento,
    string? Bairro, string? Cep);
public record ClienteEdicaoDto(string Nome, int CidadeId, string? Documento,
    string? Telefone, string? Email, string? Logradouro, string? Numero,
    string? Complemento, string? Bairro, string? Cep);
public record RotaItemDto(int ClienteId, int CodigoVisita, int OrdemVisita);
public record RotaProgramadaDto(DateTime VigenciaEmUtc, List<RotaItemDto> Itens);
