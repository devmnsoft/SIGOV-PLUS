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
version_matches=re.findall(r'ACTIONLINT_VERSION:\s*(v\d+\.\d+\.\d+)|ACTIONLINT_VERSION=(v\d+\.\d+\.\d+)', text)
versions={v for pair in version_matches for v in pair if v}
if not versions:
    errors.append('ACTIONLINT_VERSION with exact vX.Y.Z pin is required')
elif versions != {'v1.7.7'}:
    errors.append('ACTIONLINT_VERSION must be exactly v1.7.7')
if re.search(r'actionlint[^\n]*(latest|@main|@master)', text, re.I):
    errors.append('actionlint latest/main/master references are forbidden')
if 'github.com/rhysd/actionlint/cmd/actionlint@${ACTIONLINT_VERSION}' not in text:
    errors.append('go install must use github.com/rhysd/actionlint/cmd/actionlint@${ACTIONLINT_VERSION}')
if not re.search(r'uses:\s*actions/setup-go@[0-9a-f]{40}\b', text):
    errors.append('actions/setup-go must be pinned to a full commit SHA')
if 'actionlint" -version' not in text and 'actionlint -version' not in text:
    errors.append('actionlint -version must be logged')
if 'workflow-integrity-install.log' not in text:
    errors.append('workflow-integrity-install.log artifact is required')
if 'workflow-integrity.log' not in text:
    errors.append('workflow-integrity.log artifact is required')
if 'workflow-integrity-result.json' not in text:
    errors.append('workflow-integrity-result.json artifact is required')
if 'ACTIONLINT_SHA256=' in text or 'sha256sum -c -' in text or 'actionlint_${ACTIONLINT_VERSION}_linux_amd64.tar.gz' in text:
    errors.append('manual actionlint tarball/checksum bootstrap is forbidden')
for flt in re.findall(r'--filter\s+"FullyQualifiedName~([^"]+)"', text):
    if not any(flt in p.read_text(encoding='utf-8', errors='ignore') for p in Path('tests').rglob('*.cs')):
        errors.append(f'test filter has no matching test source: {flt}')
if re.search(r'1\.0\.0-rc2[0-6]\b|pos-rc-2[0-6]\b', text, re.I): errors.append('active workflow contains hardcoded old RC references')
if errors: print('\n'.join(errors)); sys.exit(1)
print('workflow integrity: PASS')
