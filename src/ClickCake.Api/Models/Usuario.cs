namespace ClickCake.Api.Models;

public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Login { get; set; } = "";
    public string SenhaHash { get; set; } = "";
    public string Perfil { get; set; } = "";
    public bool Ativo { get; set; }
}