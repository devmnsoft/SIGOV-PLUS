#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

fail=0
check_absent() { if rg -n "$1" "$2"; then echo "FAIL: $3" >&2; fail=1; fi; }
check_absent 'sigov\.compras_(fornecedor|requisicao|requisicao_item|aprovacao|cotacao(_convite|_resposta_item)?|pedido|recebimento|fatura|devolucao|fornecedor_avaliacao|historico|idempotencia|numeracao)\b' \
  src/Sigov.Infrastructure/ComprasEmpresariais 'ComprasEmpresariais ainda acessa nomes governamentais.'
check_absent 'sigov\.compras_(solicitacao|solicitacao_item|processo|ordem_compra|integracao_financeira)\b' \
  src/Sigov.Infrastructure/Bloco6 'Bloco6 ainda acessa nomes governamentais.'

python3 - <<'PY'
import hashlib,json,pathlib,sys
root=pathlib.Path('.')
d=json.loads((root/'database/postgres/migrations/manifest.json').read_text())
seen_version=set(); seen_file=set(); errors=[]
for m in d['migrations']:
    if m['version'] in seen_version: errors.append('versão duplicada '+m['version'])
    if m['file'] in seen_file: errors.append('arquivo duplicado '+m['file'])
    seen_version.add(m['version']); seen_file.add(m['file'])
    path=root/'database/postgres/migrations'/m['file']
    normalized=path.read_text().lstrip('\ufeff').replace('\r\n','\n').replace('\r','\n')
    if hashlib.sha256(normalized.encode()).hexdigest()!=m['checksum']: errors.append('checksum divergente '+m['file'])
for version in ('20260824200000','20260903173000'):
    m=next(x for x in d['migrations'] if x['version']==version)
    if not any(x['file']=='070_separate_compras_uuid_contracts.sql' for x in m.get('compatibilityBefore',[])):
        errors.append('separação UUID ausente antes de '+version)
legacy=next(x for x in d['migrations'] if x['version']=='20260831230000')
if not any(x['file']=='072_prepare_rc50_85_compras_bigint.sql' for x in legacy.get('compatibilityBefore',[])):
    errors.append('preparo bigint ausente antes de 20260831230000')
if errors:
    print('\n'.join('FAIL: '+e for e in errors),file=sys.stderr); sys.exit(1)
PY

(( fail == 0 )) || exit 1
echo 'Contratos SQL de Compras estão fisicamente isolados.'
