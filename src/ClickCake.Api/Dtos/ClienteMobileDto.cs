namespace ClickCake.Api.Dtos;

public sealed record ClienteMobileDto(
    string Nome,
    int CidadeId,
    string? Documento,
    string? Telefone,
    string? Email,
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? Cep);
