# ClickCake — Componente 5 (visitas, pedidos e envio)

Base utilizada: repositório público no commit `bcda62c` (Componente 4), em 27/10/2026.

## Instalação

1. Faça uma cópia do seu projeto e confirme que não há edições locais conflitantes.
2. Extraia o ZIP na raiz do projeto ClickCake. A pasta `src/` do pacote deve se sobrepor à pasta `src/` atual.
3. Substitua apenas os arquivos presentes neste pacote. Os demais componentes devem permanecer intactos.
4. Não é necessário executar SQL estrutural: o Componente 5 usa as tabelas já existentes no `database/01_schema.sql` e `database/02_rotas_programadas.sql`.
5. Não há pacotes NuGet adicionais; permanece o `sqlite-net-pcl` do Componente 4.
6. Não publique o projeto nem configure credenciais no GitHub antes de validar localmente.

## Arquivos entregues

Novos:

- `src/ClickCake.Api/Dtos/PedidoGeralEnvioDtos.cs`
- `src/ClickCake.Api/Controllers/PedidosMobileController.cs`
- `src/ClickCake.Mobile/Models/PedidoRascunhoModelos.cs`
- `src/ClickCake.Mobile/Data/DadosLocais.Pedidos.cs`
- `src/ClickCake.Mobile/MainPage.Pedidos.cs`

Atualizados, em versões completas:

- `src/ClickCake.Mobile/Data/DadosLocais.cs`
- `src/ClickCake.Mobile/Services/ApiCliente.cs`
- `src/ClickCake.Mobile/MainPage.xaml`
- `src/ClickCake.Mobile/MainPage.xaml.cs`

## Responsabilidades implementadas

- Rascunhos locais separados pelo banco de cada vendedor, com rota congelada, vários ciclos e apenas um ciclo ativo para atendimento.
- Visitas em ordem, pulo com motivo, retomada após interrupção e ciclo vazio encerrado localmente.
- Carrinho, categorias, quantidades, condição e forma por parcela, valores previstos, edição e exclusão com motivo.
- Envio autenticado com a mesma identificação entre tentativas. Estado `Enviando` não libera edição até haver resposta conclusiva.
- API confere perfil do vendedor, autoria da rota, conclusão de todas as visitas, valores, produtos, condição e formas de pagamento. Recebe o lote em uma transação MySQL.
- Reenvio idêntico com o mesmo GUID devolve o lote já persistido; reutilizar GUID com conteúdo diferente é rejeitado.
- **Não** cria Venda, ItemVenda, ContaReceber, reserva nem baixa estoque (Componente 6).

## Compilação mínima (Windows)

No terminal da raiz do projeto, com .NET 10 e workload MAUI instalado:

```powershell
dotnet restore ClickCake.sln
dotnet build src/ClickCake.Api/ClickCake.Api.csproj
dotnet build src/ClickCake.Web/ClickCake.Web.csproj
dotnet build src/ClickCake.Mobile/ClickCake.Mobile.csproj -f net10.0-android
```

Não é necessário executar `F5` no celular agora. Se houver erro de compilação, enviar a mensagem real do Visual Studio/terminal antes de prosseguir.

## Verificação mínima da API

1. Inicie a API no ambiente Development com banco configurado, como nos componentes anteriores.
2. `GET /api/mobile/pedidosgerais/{guid}` sem JWT deve retornar `401 Unauthorized`.
3. O mesmo GET com token de vendedor e um GUID desconhecido deve retornar `404 Not Found`.
4. O POST exige um ciclo completo de visitas (pelo menos um pedido). Um lote vazio deve ser rejeitado sem gravar linhas.

## Cenários reservados para o Componente 8

- Instalação e login no Android físico via USB; `adb reverse` e conexão HTTP local de teste.
- Fechar/abrir o aplicativo durante a rota e verificar retomada exata pelo SQLite.
- Pular um cliente, registrar pedido em outro e verificar que não é possível atender fora de ordem.
- Criar vários ciclos no mesmo dia, pausar um, retomar, editar/remover pedido e concluir o roteiro.
- Enviar lote, interromper a resposta de rede, consultar/repetir GUID e confirmar ausência de duplicações no MySQL.
- Alterar uma rota programada e confirmar que um ciclo iniciado antes preserva a ordem congelada.
- Validar rejeição de divergência de preço, correção do rascunho e geração de novo identificador.

## Estado de validação

- O XAML foi verificado quanto à sintaxe XML e os eventos referenciados foram encontrados no código.
- `git diff --check` não encontrou erros de whitespace nas alterações rastreadas.
- **Compilação .NET, execução, banco e Android não foram testados neste ambiente.** Não marcar o Componente 5 como validado até compilar no Windows e obter evidências das verificações mínimas.

RF04 (cadastro online de cliente), RF08 (histórico/repetição de itens) e RF23 (resultados/cortes no Mobile) ainda não foram implementados neste pacote; devem ser contemplados nas próximas etapas sem alterar este fluxo.
