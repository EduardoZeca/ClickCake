-- CLICKCAKE
-- Componente 1 - Estrutura inicial do MySQL
-- MySQL 8.4
-- Executar uma vez em uma instalacao nova.

CREATE DATABASE clickcake
CHARACTER SET utf8mb4
COLLATE utf8mb4_0900_ai_ci;

USE clickcake;

CREATE TABLE Usuario (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    Nome VARCHAR(150) NOT NULL,
    Login VARCHAR(150) NOT NULL,
    SenhaHash VARCHAR(500) NOT NULL,
    Perfil VARCHAR(20) NOT NULL,
    Ativo BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT uq_usuario_login UNIQUE (Login),
    CONSTRAINT ck_usuario_perfil
        CHECK (Perfil IN ('Administrador', 'Vendedor'))
) ENGINE=InnoDB;

CREATE TABLE Estado (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    Nome VARCHAR(100) NOT NULL,
    Sigla CHAR(2) NOT NULL,

    CONSTRAINT uq_estado_sigla UNIQUE (Sigla)
) ENGINE=InnoDB;

CREATE TABLE Cidade (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    EstadoId INT UNSIGNED NOT NULL,
    Nome VARCHAR(150) NOT NULL,

    CONSTRAINT fk_cidade_estado
        FOREIGN KEY (EstadoId) REFERENCES Estado(Id),

    CONSTRAINT uq_cidade_estado_nome
        UNIQUE (EstadoId, Nome)
) ENGINE=InnoDB;

CREATE TABLE Cliente (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    VendedorId INT UNSIGNED NOT NULL,
    CodigoVisita INT UNSIGNED NOT NULL,
    OrdemVisita INT UNSIGNED NOT NULL,

    OrdemVisitaProgramada INT UNSIGNED NULL,
    VigenciaOrdemEm DATETIME(6) NULL,

    Nome VARCHAR(150) NOT NULL,
    Documento VARCHAR(20) NULL,
    Telefone VARCHAR(20) NULL,
    Email VARCHAR(150) NULL,

    Logradouro VARCHAR(200) NULL,
    Numero VARCHAR(20) NULL,
    Complemento VARCHAR(100) NULL,
    Bairro VARCHAR(100) NULL,
    Cep VARCHAR(10) NULL,
    CidadeId INT UNSIGNED NOT NULL,

    Ativo BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT fk_cliente_vendedor
        FOREIGN KEY (VendedorId) REFERENCES Usuario(Id),

    CONSTRAINT fk_cliente_cidade
        FOREIGN KEY (CidadeId) REFERENCES Cidade(Id),

    CONSTRAINT uq_cliente_codigo
        UNIQUE (VendedorId, CodigoVisita),

    CONSTRAINT uq_cliente_ordem
        UNIQUE (VendedorId, OrdemVisita),

    CONSTRAINT ck_cliente_codigo
        CHECK (CodigoVisita > 0),

    CONSTRAINT ck_cliente_ordem
        CHECK (OrdemVisita > 0),

    CONSTRAINT ck_cliente_rota_programada
        CHECK (
            (OrdemVisitaProgramada IS NULL
             AND VigenciaOrdemEm IS NULL)
            OR
            (OrdemVisitaProgramada IS NOT NULL
             AND VigenciaOrdemEm IS NOT NULL
             AND OrdemVisitaProgramada > 0)
        )
) ENGINE=InnoDB;

CREATE TABLE Categoria (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    Nome VARCHAR(100) NOT NULL,
    Ativo BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT uq_categoria_nome UNIQUE (Nome)
) ENGINE=InnoDB;

CREATE TABLE Produto (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    CategoriaId INT UNSIGNED NOT NULL,

    Nome VARCHAR(150) NOT NULL,
    Descricao VARCHAR(500) NULL,

    Preco DECIMAL(12,2) NOT NULL,
    Estoque INT UNSIGNED NOT NULL DEFAULT 0,
    DataValidade DATE NOT NULL,

    Ativo BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT fk_produto_categoria
        FOREIGN KEY (CategoriaId) REFERENCES Categoria(Id),

    CONSTRAINT ck_produto_preco
        CHECK (Preco >= 0),

    CONSTRAINT ck_produto_estoque
        CHECK (Estoque >= 0),

    INDEX ix_produto_validade (DataValidade)
) ENGINE=InnoDB;

CREATE TABLE CondicaoPagamento (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    Nome VARCHAR(50) NOT NULL,
    QuantidadeParcelas INT NOT NULL,
    PrazosDias VARCHAR(30) NOT NULL,
    DescontoAVista DECIMAL(5,4) NOT NULL DEFAULT 0,

    CONSTRAINT uq_condicao_nome UNIQUE (Nome),

    CONSTRAINT ck_condicao_parcelas
        CHECK (QuantidadeParcelas BETWEEN 1 AND 3),

    CONSTRAINT ck_condicao_desconto
        CHECK (DescontoAVista BETWEEN 0 AND 1)
) ENGINE=InnoDB;

CREATE TABLE FormaPagamento (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    Nome VARCHAR(50) NOT NULL,
    Ativo BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT uq_forma_nome UNIQUE (Nome)
) ENGINE=InnoDB;

CREATE TABLE PedidoGeral (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    VendedorId INT UNSIGNED NOT NULL,

    IdentificadorEnvio CHAR(36) NOT NULL,
    HashConteudo CHAR(64) NOT NULL,

    DataCriacao DATETIME(6) NOT NULL,
    DataEnvio DATETIME(6) NOT NULL,
    DataConfirmacao DATETIME(6) NULL,

    Situacao VARCHAR(20) NOT NULL DEFAULT 'Recebido',
    MotivoCancelamento VARCHAR(500) NULL,

    CONSTRAINT fk_pedidogeral_vendedor
        FOREIGN KEY (VendedorId) REFERENCES Usuario(Id),

    CONSTRAINT uq_pedidogeral_envio
        UNIQUE (IdentificadorEnvio),

    CONSTRAINT ck_pedidogeral_situacao
        CHECK (
            Situacao IN ('Recebido', 'Confirmado', 'Cancelado')
        ),

    INDEX ix_pedidogeral_vendedor
        (VendedorId, DataEnvio)
) ENGINE=InnoDB;

CREATE TABLE Visita (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    PedidoGeralId INT UNSIGNED NOT NULL,
    ClienteId INT UNSIGNED NOT NULL,

    OrdemSnapshot INT UNSIGNED NOT NULL,
    CodigoVisitaSnapshot INT UNSIGNED NOT NULL,

    Situacao VARCHAR(20) NOT NULL,
    MotivoPulo VARCHAR(30) NULL,
    Observacao VARCHAR(500) NULL,

    CONSTRAINT fk_visita_pedidogeral
        FOREIGN KEY (PedidoGeralId) REFERENCES PedidoGeral(Id),

    CONSTRAINT fk_visita_cliente
        FOREIGN KEY (ClienteId) REFERENCES Cliente(Id),

    CONSTRAINT uq_visita_cliente
        UNIQUE (PedidoGeralId, ClienteId),

    CONSTRAINT uq_visita_ordem
        UNIQUE (PedidoGeralId, OrdemSnapshot),

    CONSTRAINT uq_visita_referencia
        UNIQUE (Id, PedidoGeralId, ClienteId),

    CONSTRAINT ck_visita_situacao
        CHECK (
            Situacao IN ('PedidoRegistrado', 'Pulado')
        ),

    CONSTRAINT ck_visita_motivo
        CHECK (
            (
                Situacao = 'PedidoRegistrado'
                AND MotivoPulo IS NULL
            )
            OR
            (
                Situacao = 'Pulado'
                AND MotivoPulo IN (
                    'NaoFaraPedido',
                    'LocalFechado',
                    'Indisponivel',
                    'Outro'
                )
                AND (
                    MotivoPulo <> 'Outro'
                    OR NULLIF(TRIM(Observacao), '') IS NOT NULL
                )
            )
        )
) ENGINE=InnoDB;

CREATE TABLE Pedido (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,

    PedidoGeralId INT UNSIGNED NOT NULL,
    ClienteId INT UNSIGNED NOT NULL,
    VisitaId INT UNSIGNED NOT NULL,
    CondicaoPagamentoId INT UNSIGNED NOT NULL,

    Situacao VARCHAR(20) NOT NULL DEFAULT 'Recebido',

    TotalPrevisto DECIMAL(12,2) NOT NULL,
    TotalFinal DECIMAL(12,2) NULL,

    CONSTRAINT fk_pedido_pedidogeral
        FOREIGN KEY (PedidoGeralId) REFERENCES PedidoGeral(Id),

    CONSTRAINT fk_pedido_cliente
        FOREIGN KEY (ClienteId) REFERENCES Cliente(Id),

    CONSTRAINT fk_pedido_visita
        FOREIGN KEY (
            VisitaId, PedidoGeralId, ClienteId
        )
        REFERENCES Visita (
            Id, PedidoGeralId, ClienteId
        ),

    CONSTRAINT fk_pedido_condicao
        FOREIGN KEY (CondicaoPagamentoId)
        REFERENCES CondicaoPagamento(Id),

    CONSTRAINT uq_pedido_visita UNIQUE (VisitaId),

    CONSTRAINT uq_pedido_cliente_lote
        UNIQUE (PedidoGeralId, ClienteId),

    CONSTRAINT ck_pedido_situacao
        CHECK (
            Situacao IN (
                'Recebido',
                'AtendidoTotal',
                'AtendidoParcial',
                'NaoAtendido',
                'Cancelado'
            )
        ),

    CONSTRAINT ck_pedido_valores
        CHECK (
            TotalPrevisto >= 0
            AND (TotalFinal IS NULL OR TotalFinal >= 0)
        )
) ENGINE=InnoDB;

CREATE TABLE ItemPedido (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,

    PedidoId INT UNSIGNED NOT NULL,
    ProdutoId INT UNSIGNED NOT NULL,

    NomeProdutoSnapshot VARCHAR(150) NOT NULL,
    PrecoUnitarioSnapshot DECIMAL(12,2) NOT NULL,

    QuantidadeSolicitada INT UNSIGNED NOT NULL,
    QuantidadeAtendida INT UNSIGNED NULL,

    CONSTRAINT fk_itempedido_pedido
        FOREIGN KEY (PedidoId) REFERENCES Pedido(Id),

    CONSTRAINT fk_itempedido_produto
        FOREIGN KEY (ProdutoId) REFERENCES Produto(Id),

    CONSTRAINT uq_itempedido_produto
        UNIQUE (PedidoId, ProdutoId),

    CONSTRAINT ck_itempedido_quantidades
        CHECK (
            QuantidadeSolicitada > 0
            AND (
                QuantidadeAtendida IS NULL
                OR QuantidadeAtendida <= QuantidadeSolicitada
            )
        ),

    CONSTRAINT ck_itempedido_preco
        CHECK (PrecoUnitarioSnapshot >= 0)
) ENGINE=InnoDB;

CREATE TABLE ParcelaPedido (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,

    PedidoId INT UNSIGNED NOT NULL,
    Numero INT NOT NULL,
    PrazoDias INT NOT NULL,
    FormaPagamentoId INT UNSIGNED NOT NULL,

    ValorPrevisto DECIMAL(12,2) NOT NULL,

    CONSTRAINT fk_parcelapedido_pedido
        FOREIGN KEY (PedidoId) REFERENCES Pedido(Id),

    CONSTRAINT fk_parcelapedido_forma
        FOREIGN KEY (FormaPagamentoId)
        REFERENCES FormaPagamento(Id),

    CONSTRAINT uq_parcelapedido_numero
        UNIQUE (PedidoId, Numero),

    CONSTRAINT ck_parcelapedido_numero
        CHECK (Numero BETWEEN 1 AND 3),

    CONSTRAINT ck_parcelapedido_prazo
        CHECK (PrazoDias IN (0, 7, 14, 21)),

    CONSTRAINT ck_parcelapedido_valor
        CHECK (ValorPrevisto >= 0)
) ENGINE=InnoDB;

CREATE TABLE Venda (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,

    PedidoId INT UNSIGNED NOT NULL,
    DataConfirmacao DATETIME(6) NOT NULL,
    TotalFinal DECIMAL(12,2) NOT NULL,

    CONSTRAINT fk_venda_pedido
        FOREIGN KEY (PedidoId) REFERENCES Pedido(Id),

    CONSTRAINT uq_venda_pedido UNIQUE (PedidoId),

    CONSTRAINT ck_venda_total
        CHECK (TotalFinal > 0)
) ENGINE=InnoDB;

CREATE TABLE ItemVenda (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,

    VendaId INT UNSIGNED NOT NULL,
    ProdutoId INT UNSIGNED NOT NULL,

    Quantidade INT UNSIGNED NOT NULL,
    PrecoUnitario DECIMAL(12,2) NOT NULL,

    CONSTRAINT fk_itemvenda_venda
        FOREIGN KEY (VendaId) REFERENCES Venda(Id),

    CONSTRAINT fk_itemvenda_produto
        FOREIGN KEY (ProdutoId) REFERENCES Produto(Id),

    CONSTRAINT uq_itemvenda_produto
        UNIQUE (VendaId, ProdutoId),

    CONSTRAINT ck_itemvenda_quantidade
        CHECK (Quantidade > 0),

    CONSTRAINT ck_itemvenda_preco
        CHECK (PrecoUnitario >= 0)
) ENGINE=InnoDB;

CREATE TABLE ContaReceber (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,

    VendaId INT UNSIGNED NOT NULL,
    ParcelaPedidoId INT UNSIGNED NOT NULL,
    FormaPagamentoId INT UNSIGNED NOT NULL,

    NumeroParcela INT NOT NULL,
    Vencimento DATE NOT NULL,
    ValorBase DECIMAL(12,2) NOT NULL,

    Situacao VARCHAR(10) NOT NULL DEFAULT 'Aberta',
    DataRecebimento DATE NULL,

    Multa DECIMAL(12,2) NOT NULL DEFAULT 0,
    Juros DECIMAL(12,2) NOT NULL DEFAULT 0,
    DescontoAntecipacao DECIMAL(12,2) NOT NULL DEFAULT 0,
    ValorRecebido DECIMAL(12,2) NULL,

    CONSTRAINT fk_contareceber_venda
        FOREIGN KEY (VendaId) REFERENCES Venda(Id),

    CONSTRAINT fk_contareceber_parcela
        FOREIGN KEY (ParcelaPedidoId)
        REFERENCES ParcelaPedido(Id),

    CONSTRAINT fk_contareceber_forma
        FOREIGN KEY (FormaPagamentoId)
        REFERENCES FormaPagamento(Id),

    CONSTRAINT uq_contareceber_parcela
        UNIQUE (ParcelaPedidoId),

    CONSTRAINT uq_contareceber_venda_numero
        UNIQUE (VendaId, NumeroParcela),

    CONSTRAINT ck_contareceber_situacao
        CHECK (Situacao IN ('Aberta', 'Paga')),

    CONSTRAINT ck_contareceber_numero
        CHECK (NumeroParcela BETWEEN 1 AND 3),

    CONSTRAINT ck_contareceber_valores
        CHECK (
            ValorBase >= 0
            AND Multa >= 0
            AND Juros >= 0
            AND DescontoAntecipacao >= 0
            AND (
                ValorRecebido IS NULL
                OR ValorRecebido >= 0
            )
        ),

    CONSTRAINT ck_contareceber_recebimento
        CHECK (
            (
                Situacao = 'Aberta'
                AND DataRecebimento IS NULL
                AND ValorRecebido IS NULL
            )
            OR
            (
                Situacao = 'Paga'
                AND DataRecebimento IS NOT NULL
                AND ValorRecebido IS NOT NULL
            )
        ),

    INDEX ix_contareceber_vencimento
        (Situacao, Vencimento)
) ENGINE=InnoDB;

INSERT INTO CondicaoPagamento
    (Id, Nome, QuantidadeParcelas, PrazosDias, DescontoAVista)
VALUES
    (1, 'À vista', 1, '0', 0.1800),
    (2, '7/14 dias', 2, '7,14', 0.0000),
    (3, '7/14/21 dias', 3, '7,14,21', 0.0000);

INSERT INTO FormaPagamento (Id, Nome, Ativo)
VALUES
    (1, 'Pix', TRUE),
    (2, 'Boleto', TRUE),
    (3, 'Dinheiro', TRUE),
    (4, 'Crédito', TRUE),
    (5, 'Débito', TRUE);