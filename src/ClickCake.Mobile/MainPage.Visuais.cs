using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ClickCake.Mobile;

public partial class MainPage
{
    private void MelhorarSeletores()
    {
        foreach (var seletor in new[]
        {
            EscolhaCategoria, EscolhaProduto, EscolhaCondicao, EscolhaMotivo
        })
            EstilizarSeletor(seletor);

        EntradaQuantidade.BackgroundColor = Color.FromArgb("#EFF6FF");
        EntradaQuantidade.HeightRequest = 48;
    }

    private static void EstilizarSeletor(Picker seletor)
    {
        seletor.BackgroundColor = Color.FromArgb("#DBEAFE");
        seletor.TextColor = Color.FromArgb("#0F172A");
        seletor.TitleColor = Color.FromArgb("#334155");
        seletor.HeightRequest = 48;
    }
}
