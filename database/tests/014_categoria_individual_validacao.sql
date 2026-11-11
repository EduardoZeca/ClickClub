-- ClickClub — validação somente leitura: categoria Individual.
-- Consulta vínculos ativos legados que exigem regularização manual.
-- Resultado pode conter linhas antigas: não excluí-las automaticamente.
USE clickclub_db;

SELECT a.id AS associacao_id,
       a.matricula,
       c.nome AS categoria,
       COUNT(d.id) AS dependentes_ativos
FROM associacao a
JOIN categoria_associativa c ON c.id=a.categoria_id
JOIN dependente_associacao d
    ON d.associacao_id=a.id AND d.ativo=TRUE
WHERE LOWER(TRIM(c.nome))='individual'
GROUP BY a.id, a.matricula, c.nome
ORDER BY a.id;
