#!/usr/bin/env python3
import re, sys
from pathlib import Path
workflow = Path(sys.argv[1]); text = workflow.read_text(encoding='utf-8')
# O actionlint executado antes deste script valida a sintaxe YAML completa.
# Este validador complementar usa uma leitura estrutural sem dependências externas.
jobs_text=text.split('\njobs:',1)[1] if '\njobs:' in text else text
job_matches=list(re.finditer(r'^  ([A-Za-z0-9_-]+):\n(?=(?:    |  [A-Za-z0-9_-]+:|$))', jobs_text, re.M))
jobs={}
for idx,m in enumerate(job_matches):
    start=m.end(); end=job_matches[idx+1].start() if idx+1 < len(job_matches) else len(jobs_text)
    jobs[m.group(1)]={'__body': jobs_text[start:end]}
if len(jobs) != len(job_matches): print('Duplicate job id detected'); sys.exit(1)
required=set('workflow-integrity release-context build-test dependency-injection web-smoke powershell-lint migrations-manifest seed-idempotency script-completop-validate script-completop-idempotency schema-equivalence standalone-postgres-runtime docker-build docker-compose-e2e ui-contrast release-package-check go-live-check'.split())
errors=[]
missing=sorted(required-set(jobs))
if missing: errors.append('Missing required jobs: '+', '.join(missing))
for forbidden in ['tests/Sigov.Tests/Sigov.Tests.csproj','tarefas-postgres','tarefas-api-e2e','tarefas-ui-e2e','FullyQualifiedName~UiSmoke','FullyQualifiedName~TarefasApi','FullyQualifiedName~TarefasUi']:
    if forbidden in text: errors.append(f'Forbidden Pós-RC 27A gate/reference found: {forbidden}')
artifact_names=set(); graph={j:set() for j in jobs}
script_pattern=re.compile(r'(?P<script>(?:\.\/)?scripts\/[A-Za-z0-9_\-.\/]+\.(?:ps1|sh|cmd|py|mjs|sql))')
csproj_pattern=re.compile(r'(?P<csproj>(?:src|tests)\/[A-Za-z0-9_\-.\/]+\.csproj)')
for job_id, job in jobs.items():
    body=job['__body']
    if 'timeout-minutes:' not in body: errors.append(f'{job_id}: timeout-minutes is required')
    if re.search(r'continue-on-error:\s*true', body): errors.append(f'{job_id}: continue-on-error is forbidden')
    needs=[]
    mn=re.search(r'^    needs:\s*(.+)$', body, re.M)
    if mn:
        raw=mn.group(1).strip()
        if raw.startswith('['): needs=[x.strip() for x in raw.strip('[]').split(',') if x.strip()]
        else: needs=[raw]
    for n in needs:
        if n not in jobs: errors.append(f'{job_id}: unknown need {n}')
        else: graph[job_id].add(n)
    uploads=body.count('uses: actions/upload-artifact@v4')
    for m in script_pattern.finditer(body):
        path=Path(m.group('script').removeprefix('./'))
        if not path.exists(): errors.append(f'{job_id}: missing script {path}')
    for m in csproj_pattern.finditer(body):
        path=Path(m.group('csproj'))
        if not path.exists(): errors.append(f'{job_id}: missing csproj {path}')
    for name in re.findall(r'name:\s*([^\n{][^\n]+)', body):
        if 'log' in name or 'artifact' in name or 'sigov-plus' in name:
            if name in artifact_names: errors.append(f'duplicate artifact name: {name}')
            artifact_names.add(name)
    if uploads==0: errors.append(f'{job_id}: upload-artifact step is required')
visiting=set(); seen=set()
def dfs(n):
    if n in visiting: errors.append(f'cycle detected at {n}'); return
    if n in seen: return
    visiting.add(n)
    for dep in graph.get(n,[]): dfs(dep)
    visiting.remove(n); seen.add(n)
for j in jobs: dfs(j)
if not re.search(r'ACTIONLINT_VERSION=\d+\.\d+\.\d+', text): errors.append('actionlint version is not pinned centrally')
if 'ACTIONLINT_SHA256=' not in text or 'sha256sum -c -' not in text: errors.append('actionlint checksum validation is required')
for flt in re.findall(r'--filter\s+"FullyQualifiedName~([^"]+)"', text):
    if not any(flt in p.read_text(encoding='utf-8', errors='ignore') for p in Path('tests').rglob('*.cs')):
        errors.append(f'test filter has no matching test source: {flt}')
if re.search(r'1\.0\.0-rc2[0-6]\b|pos-rc-2[0-6]\b', text, re.I): errors.append('active workflow contains hardcoded old RC references')
if errors: print('\n'.join(errors)); sys.exit(1)
print('workflow integrity: PASS')
