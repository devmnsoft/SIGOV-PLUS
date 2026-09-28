-- Reparo pontual do ambiente vivo (docker PG16): remove concessões demo que
-- apontavam para ids de outras áreas (694..727) e o seed idempotente recria
-- as concessões corretas resolvidas por chave de catálogo.
delete from sigov.perfil_permissao
 where perfil_acesso_id in (9001, 9002)
   and permissao_id in (694, 700, 701, 702, 703, 705, 706, 726, 727);
