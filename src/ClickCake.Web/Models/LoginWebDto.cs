using System.ComponentModel.DataAnnotations;

namespace ClickCake.Web.Models;

public class LoginWebDto
{
    [Required]
    public string Login { get; set; } = "";
    [Required]
    [DataType(DataType.Password)]
    public string Senha { get; set; } = "";
}
