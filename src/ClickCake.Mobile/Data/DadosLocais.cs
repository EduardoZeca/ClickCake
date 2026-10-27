using System.Globalization;
using ClickCake.Mobile.Models;
using Microsoft.Maui.Storage;
using SQLite;

namespace ClickCake.Mobile.Data;

public sealed partial class DadosLocais
{
    private readonly SQLiteAsyncConnection _banco;

    public DadosLocais(int vendedorId)
    {
        // Cada vendedor usa um arquivo separado: nao mistura carteiras no aparelho.
        var caminho = Path.Combine(FileSystem.AppDataDirectory, $"clickcake_{vendedorId}.db3");
        _banco = new SQLiteAsyncConnection(caminho);
    }

    public async Task InicializarAsync()
    {
        await _banco.CreateTableAsync<ClienteLocal>();
        await _banco.CreateTableAsync<RotaItemLocal>();
        await _banco.CreateTableAsync<CategoriaLocal>();
        await _banco.CreateTableAsync<ProdutoLocal>();
        await _banco.CreateTableAsync<CondicaoLocal>();
        await _banco.CreateTableAsync<FormaLocal>();
        await _banco.CreateTableAsync<ConfiguracaoLocal>();
        await _banco.CreateTableAsync<PedidoGeralRascunho>();
        await _banco.CreateTableAsync<VisitaRascunho>();
        await _banco.CreateTableAsync<PedidoRascunho>();
        await _banco.CreateTableAsync<ItemPedidoRascunho>();
        await _banco.CreateTableAsync<ParcelaRascunho>();
    }

    public async Task SalvarSincronizacaoAsync(SincronizacaoResposta dados)
    {
        // A atualizacao do cache ocorre por inteiro ou e desfeita.
        await _banco.RunInTransactionAsync(db =>
        {
            // Preserva clientes previamente conhecidos para futuros historicos.
            // Somente a associacao da rota corrente e substituida.
            db.DeleteAll<RotaItemLocal>();
            foreach (var c in dados.Clientes)
            {
                db.InsertOrReplace(new ClienteLocal
                {
                    Id = c.Id,
                    Nome = c.Nome,
                    Documento = c.Documento,
                    Telefone = c.Telefone,
                    Email = c.Email,
                    Logradouro = c.Logradouro,
                    Numero = c.Numero,
                    Complemento = c.Complemento,
                    Bairro = c.Bairro,
                    Cep = c.Cep,
                    CidadeId = c.CidadeId
                });
                db.Insert(new RotaItemLocal
                {
                    ClienteId = c.Id,
                    CodigoVisita = c.CodigoVisita,
                    OrdemVisita = c.OrdemVisita
                });
            }

            db.DeleteAll<CategoriaLocal>();
            foreach (var c in dados.Categorias)
                db.Insert(new CategoriaLocal { Id = c.Id, Nome = c.Nome });

            db.DeleteAll<ProdutoLocal>();
            foreach (var p in dados.Produtos)
                db.Insert(new ProdutoLocal
                {
                    Id = p.Id, CategoriaId = p.CategoriaId, Nome = p.Nome,
                    Descricao = p.Descricao, Preco = p.Preco, DataValidade = p.DataValidade
                });

            db.DeleteAll<CondicaoLocal>();
            foreach (var c in dados.Condicoes)
                db.Insert(new CondicaoLocal
                {
                    Id = c.Id, Nome = c.Nome, QuantidadeParcelas = c.QuantidadeParcelas,
                    PrazosDias = c.PrazosDias, DescontoAVista = c.DescontoAVista
                });

            db.DeleteAll<FormaLocal>();
            foreach (var f in dados.Formas)
                db.Insert(new FormaLocal { Id = f.Id, Nome = f.Nome });

            db.InsertOrReplace(new ConfiguracaoLocal
            {
                Chave = "UltimaSincronizacaoUtc",
                Valor = dados.ServidorEmUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
            });
            db.InsertOrReplace(new ConfiguracaoLocal
            {
                Chave = "RotaVersaoId",
                Valor = dados.RotaVersaoId?.ToString(CultureInfo.InvariantCulture) ?? "original"
            });
        });
    }

    public async Task<List<ClienteExibicao>> ListarRotaAsync()
    {
        var itens = await _banco.Table<RotaItemLocal>()
            .OrderBy(x => x.OrdemVisita).ToListAsync();
        var lista = new List<ClienteExibicao>();
        foreach (var item in itens)
        {
            var cliente = await _banco.FindAsync<ClienteLocal>(item.ClienteId);
            if (cliente == null) continue;
            lista.Add(new ClienteExibicao
            {
                Nome = cliente.Nome,
                CodigoVisita = item.CodigoVisita,
                OrdemVisita = item.OrdemVisita,
                Telefone = cliente.Telefone
            });
        }
        return lista;
    }

    public async Task<List<ProdutoExibicao>> ListarProdutosAsync()
    {
        var produtos = await _banco.Table<ProdutoLocal>().OrderBy(x => x.Nome).ToListAsync();
        return produtos.Select(x => new ProdutoExibicao
        {
            Nome = x.Nome, Preco = x.Preco, DataValidade = x.DataValidade
        }).ToList();
    }

    public async Task<List<CondicaoExibicao>> ListarCondicoesAsync()
    {
        var condicoes = await _banco.Table<CondicaoLocal>().OrderBy(x => x.Id).ToListAsync();
        return condicoes.Select(x => new CondicaoExibicao
        {
            Nome = x.Nome, QuantidadeParcelas = x.QuantidadeParcelas,
            PrazosDias = x.PrazosDias
        }).ToList();
    }

    public async Task<string> ObterResumoSincronizacaoAsync()
    {
        var data = await _banco.FindAsync<ConfiguracaoLocal>("UltimaSincronizacaoUtc");
        var versao = await _banco.FindAsync<ConfiguracaoLocal>("RotaVersaoId");
        if (data == null) return "Nenhuma sincronizacao realizada neste aparelho.";

        var instante = DateTime.Parse(data.Valor, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind).ToLocalTime();
        var roteiro = versao?.Valor == "original" ? "original" : $"versao {versao?.Valor}";
        return $"Ultima sincronizacao: {instante:dd/MM/yyyy HH:mm} | Rota: {roteiro}";
    }
}
