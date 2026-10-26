-- ClickCake - Componente 3
-- Executar uma vez no banco ja criado pelo 01_schema.sql.
-- Versoes completas da carteira/rota, com vigencia futura.

USE clickcake;

CREATE TABLE RotaVersao (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    VendedorId INT UNSIGNED NOT NULL,
    CriadaEm DATETIME(6) NOT NULL,
    VigenciaEm DATETIME(6) NOT NULL,
    CONSTRAINT fk_rotaversao_vendedor
        FOREIGN KEY (VendedorId) REFERENCES Usuario(Id),
    CONSTRAINT uq_rotaversao_vigencia
        UNIQUE (VendedorId, VigenciaEm),
    INDEX ix_rotaversao_busca (VendedorId, VigenciaEm)
) ENGINE=InnoDB;

CREATE TABLE RotaVersaoItem (
    Id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    RotaVersaoId INT UNSIGNED NOT NULL,
    ClienteId INT UNSIGNED NOT NULL,
    OrdemVisita INT UNSIGNED NOT NULL,
    CodigoVisita INT UNSIGNED NOT NULL,
    CONSTRAINT fk_rotaitem_versao
        FOREIGN KEY (RotaVersaoId) REFERENCES RotaVersao(Id),
    CONSTRAINT fk_rotaitem_cliente
        FOREIGN KEY (ClienteId) REFERENCES Cliente(Id),
    CONSTRAINT uq_rotaitem_cliente
        UNIQUE (RotaVersaoId, ClienteId),
    CONSTRAINT uq_rotaitem_ordem
        UNIQUE (RotaVersaoId, OrdemVisita),
    CONSTRAINT uq_rotaitem_codigo
        UNIQUE (RotaVersaoId, CodigoVisita),
    CONSTRAINT ck_rotaitem_ordem CHECK (OrdemVisita > 0),
    CONSTRAINT ck_rotaitem_codigo CHECK (CodigoVisita > 0)
) ENGINE=InnoDB;
