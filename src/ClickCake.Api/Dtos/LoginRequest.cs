using System.ComponentModel.DataAnnotations;

namespace ClickCake.Api.Dtos;

public class LoginRequest
{
    [Required]
    public string Login { get; set; } = "";

    [Required]
    public string Senha { get; set; } = "";
}