-- =============================================================================
-- SIGOV PLUS · Ciclo 1 (Educação Avançada) · TIER2 — fixture idempotente
-- =============================================================================
-- Fixture de desenvolvimento/homologação (regra 9: SOMENTE dados fictícios,
-- idempotente; regra 10: sem senha/token/chave/dado pessoal real).
--
-- Popula sigov.educacao_biblioteca_acervo no tenant 910001 com 120 linhas
-- marcadas (codigo LIKE 'c1e26-%'), distribuição determinística de status
-- (60 ATIVA / 20 RASCUNHO / 20 ATRASADO / 20 ENCERRADA) + 2 linhas de conteúdo
-- exigindo escape CSV/proteção de fórmula. Usado por scripts/c1_tier2_e2e.ps1
-- que REMOVE o fixture ao final (delete pelo marcador).
-- =============================================================================
delete from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'c1e26-%';
insert into sigov.educacao_biblioteca_acervo (tenant_id,codigo,nome,status,tipo,quantidade,valor)
select 910001, 'c1e26-'||g, 'Livro '||g,
  case when g<=60 then 'ATIVA' when g<=80 then 'RASCUNHO' when g<=100 then 'ATRASADO' else 'ENCERRADA' end,
  'ACERVO', g::numeric, g::numeric*0.75
from generate_series(1,120) g;
-- Conteudo que exige escape CSV + protecao de formula (linhas deterministicas)
update sigov.educacao_biblioteca_acervo
   set nome='=CMD(); "a"; b'
 where tenant_id=910001 and codigo=(select min(codigo) from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'c1e26-%');
update sigov.educacao_biblioteca_acervo
   set nome='linha1' || chr(10) || 'linha2'
 where tenant_id=910001 and codigo=(select codigo from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'c1e26-%' order by codigo limit 1 offset 1);
