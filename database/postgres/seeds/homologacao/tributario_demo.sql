do $$
begin
    if current_setting('sigov.environment', true) = 'Production' then
        raise exception 'Seed de homologação tributário_demo.sql bloqueado em Production';
    end if;
end $$;

insert into sigov.contribuinte (tenant_id, inscricao, nome, documento, tipo_pessoa, email, telefone, consentimento_lgpd)
select t.id, seed.inscricao, seed.nome, seed.documento, seed.tipo_pessoa, seed.email, seed.telefone, true
from sigov.tenant t
cross join (values
    ('HOM-IPTU-000001','Contribuinte Homologação IPTU','00000000191','FISICA','iptu.homologacao@sigov.local','(00) 3000-0001'),
    ('HOM-ISS-000002','Prestador Homologação ISS','00000000000191','JURIDICA','iss.homologacao@sigov.local','(00) 3000-0002')
) as seed(inscricao,nome,documento,tipo_pessoa,email,telefone)
where t.slug = 'plataforma-global'
on conflict (tenant_id, inscricao) do update set nome=excluded.nome, email=excluded.email, telefone=excluded.telefone, updated_at=now();
