# ClickCake — Distribuidora de panificacao

Projeto academico integrado de Programacao Web e Mobile. A loja administra produtos,
clientes, vendedores, rotas, pedidos, estoque e recebimentos. O vendedor percorre a
rota sequencial no aplicativo Android, salva rascunhos SQLite e envia os lotes a API.

## Tecnologias

- API: C# / ASP.NET Core Web API (.NET 10), JWT e MySQL.
- Web: ASP.NET Core MVC (.NET 10), consumindo exclusivamente a API.
- Mobile: .NET MAUI 10 para Android, com SQLite por vendedor.
- Ferramentas gratuitas: .NET SDK 10, VS Code, Visual Studio com MAUI e MySQL.

## Requisitos para executar

1. .NET SDK 10 e ferramentas de compilacao compativeis.
2. MySQL Server em execucao; usuario proprio da API com `SELECT`, `INSERT`,
   `UPDATE` e `DELETE` no banco `clickcake`. Criacao de tabelas fica com o
   administrador do MySQL.
3. Visual Studio com workload .NET MAUI e ferramentas Android, para o Mobile.
4. Android com depuracao USB autorizada, cabo de dados e `adb` disponivel.

## Banco de dados

No MySQL Workbench, execute **uma vez** e nesta ordem, com um usuario que possua
permissao de criar estruturas:

1. `database/01_schema.sql`
2. `database/02_rotas_programadas.sql`
3. `database/03_vendas_valor_zero.sql` (se ainda nao aplicado no Componente 6)

Nao execute novamente os scripts de criacao sobre um banco que ja funciona.
O Componente 8 nao requer migracao adicional.

## Configuracao local e seguranca

No terminal na raiz do repositorio, configure segredos da API **fora do Git**:

```powershell
dotnet user-secrets set "ConnectionStrings:ClickCake" "Server=localhost;Port=3306;Database=clickcake;User ID=clickcake_api;Password=SUA_SENHA" --project src/ClickCake.Api
dotnet user-secrets set "Jwt:Chave" "SUA_CHAVE_ALEATORIA_COM_PELO_MENOS_32_BYTES" --project src/ClickCake.Api
dotnet user-secrets set "AdminInicial:Nome" "Administrador" --project src/ClickCake.Api
dotnet user-secrets set "AdminInicial:Login" "admin" --project src/ClickCake.Api
dotnet user-secrets set "AdminInicial:Senha" "ESCOLHA_UMA_SENHA_FORTE_COM_12_OU_MAIS" --project src/ClickCake.Api
```

Substitua os valores de exemplo. O administrador inicial e criado somente
no primeiro uso em ambiente Development e apenas se ainda nao existe.
Nao publique tokens, senhas reais ou `secrets.json` no Git.

Confie no certificado local de desenvolvimento:

```powershell
dotnet dev-certs https --trust
```

## Compilar

```powershell
dotnet build src/ClickCake.Api/ClickCake.Api.csproj
dotnet build src/ClickCake.Web/ClickCake.Web.csproj
dotnet build src/ClickCake.Mobile/ClickCake.Mobile.csproj -f net10.0-android
```

## Executar API e Web no Windows

Terminal 1, na raiz do repositorio:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/ClickCake.Api --no-launch-profile --urls "https://localhost:7101;http://127.0.0.1:5101"
```

Terminal 2:

```powershell
dotnet run --project src/ClickCake.Web --launch-profile https
```

Abra `https://localhost:7172` (Web). A Web usa a API em
`https://localhost:7101`, conforme `src/ClickCake.Web/appsettings.json`.
O HTTP `127.0.0.1:5101` existe **somente** para testar o Mobile por USB.

## Executar Mobile no Android fisico

1. No aparelho, ative Opcoes do desenvolvedor e Depuracao USB.
2. Conecte o aparelho ao Windows; aceite a autorizacao de depuracao.
3. No terminal com `adb` instalado, execute:

```powershell
adb devices
adb reverse tcp:5101 tcp:5101
```

4. Deixe a API aberta na porta `5101`.
5. Abra `src/ClickCake.Mobile/ClickCake.Mobile.csproj` no Visual Studio;
   selecione o Android fisico como destino e execute em Debug.

O aplicativo usa `http://127.0.0.1:5101/` para este teste USB, portanto
**nao funciona remotamente por essa URL**. Para uso fora do laboratorio,
seria necessario configurar HTTPS e um servidor acessivel pelo aparelho.

## Fluxo resumido de demonstracao

1. Administrador entra na Web, cadastra vendedor, localidades, clientes,
   categorias, produtos com validade, formas de pagamento e programa uma rota.
2. A nova rota entra em vigor no primeiro ciclo iniciado **apos 24 horas**
   da programacao. Ciclos existentes mantem a sequencia anterior.
3. Vendedor entra no Mobile, sincroniza, cria ou retoma um ciclo, atende cada
   cliente na ordem obrigatoria ou o pula com motivo.
4. Pode consultar historico do cliente, repetir itens disponiveis e cadastrar
   cliente novo **online**. Cliente novo espera programacao pela Web para entrar
   na rota; nao aparece imediatamente no ciclo.
5. O Mobile salva pedidos e visitas no SQLite. O vendedor encerra a rota e
   envia o lote nao vazio. O envio e idempotente.
6. A Web revisa cortes, ajusta quantidades dentro do saldo e confirma ou
   cancela o lote. Confirmar cria vendas, baixa estoque e gera recebiveis.
7. No Financeiro, o administrador simula antecipacao/atraso e confirma
   manualmente o recebimento integral de cada parcela.
8. O vendedor atualiza os resultados e consulta cortes, cancelamentos e
   pagamentos; dados previamente sincronizados permanecem no SQLite offline.

### Condicoes de pagamento

| Condicao | Regra |
|---|---|
| A vista | Desconto de 18%, uma parcela |
| 7/14 | Duas parcelas nos dias 7 e 14 da confirmacao |
| 7/14/21 | Tres parcelas nos dias 7, 14 e 21 da confirmacao |

O atraso gera multa unica de 5% e juros simples de 2% por dia sobre a base;
a antecipacao gera desconto simples de 1% por dia. Recebimentos sao integrais
e manuais. Inadimplencia nao impede novos pedidos. Vendas de R$ 0,00 sao
permitidas quando ha ao menos um item efetivamente atendido.

## Cenarios de verificacao

Consulte `COMPONENTE8_APLICACAO.md` no pacote de entrega do Componente 8.
Testes conclusivos dependem da execucao real no ambiente do aluno,
especialmente do Android fisico e do MySQL.

## Limites

Projeto academico local. Nao possui gateway de pagamento, nuvem, filas,
integracao bancaria, limite de credito, cliente com login ou controle por
multiplos lotes de validade para um mesmo produto.
