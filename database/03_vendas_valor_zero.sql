-- Componente 6: permitir venda de total zero quando houver itens atendidos.
-- Executar no MySQL Workbench com usuario administrador, depois do schema inicial.
USE clickcake;

ALTER TABLE Venda DROP CHECK ck_venda_total;
ALTER TABLE Venda ADD CONSTRAINT ck_venda_total CHECK (TotalFinal >= 0);
