#!/usr/bin/env python3
import re
import sys
from pathlib import Path
from ruamel.yaml import YAML
from ruamel.yaml.constructor import DuplicateKeyError

workflow = Path(sys.argv[1])
text = workflow.read_text(encoding='utf-8')
try:
    data = YAML(typ='safe').load(text)
except DuplicateKeyError as exc:
    print(f'Duplicate YAML key: {exc}')
    sys.exit(1)

jobs = (data or {}).get('jobs') or {}
required = {
    'workflow-integrity','release-context','build-test','dependency-injection','powershell-lint',
    'migrations-manifest','seed-idempotency','script-completop-validate','script-completop-idempotency',
    'schema-equivalence','standalone-postgres-runtime','docker-build','docker-compose-e2e','ui-contrast',
    'ui-smoke','tarefas-postgres','tarefas-api-e2e','tarefas-ui-e2e','release-package-check','go-live-check'
}
missing = sorted(required - set(jobs))
if missing:
    print('Missing required jobs: ' + ', '.join(missing))
    sys.exit(1)

errors = []
artifact_names = set()
script_pattern = re.compile(r'(?P<script>(?:\.\/)?scripts\/[A-Za-z0-9_\-\.\/]+\.(?:ps1|sh|cmd|py|mjs|sql))')
placeholder_patterns = [
    re.compile(r'^\s*echo\s+["\']?[^\n]*$', re.I),
    re.compile(r'^\s*test\s+-f\s+\S+\s*$', re.I),
    re.compile(r'^\s*true\s*$', re.I),
    re.compile(r'exit\s+0\s*(?:#.*)?$', re.I),
    re.compile(r'covered by', re.I),
]
for job_id, job in jobs.items():
    if job.get('continue-on-error') is True:
        errors.append(f'{job_id}: continue-on-error is forbidden')
    needs = job.get('needs', [])
    if isinstance(needs, str):
        needs = [needs]
    for need in needs:
        if need not in jobs:
            errors.append(f'{job_id}: unknown need {need}')
    for step in job.get('steps', []):
        if step.get('continue-on-error') is True:
            errors.append(f'{job_id}: step continue-on-error is forbidden')
        run = step.get('run')
        if isinstance(run, str):
            stripped = run.strip()
            for pattern in placeholder_patterns:
                if pattern.search(stripped) and len([l for l in stripped.splitlines() if l.strip() and not l.strip().startswith('#')]) == 1:
                    errors.append(f'{job_id}: placeholder-only run detected: {stripped[:80]}')
            for match in script_pattern.finditer(run):
                script = Path(match.group('script').removeprefix('./'))
                if not script.exists():
                    errors.append(f'{job_id}: referenced script does not exist: {script}')
        uses = step.get('uses', '')
        if uses == 'actions/upload-artifact@v4':
            name = (step.get('with') or {}).get('name')
            if name:
                if name in artifact_names:
                    errors.append(f'duplicate artifact name: {name}')
                artifact_names.add(name)

if re.search(r'1\.0\.0-rc2[345]|rc23a|pos-rc-23a|pos-rc-25', text, re.I):
    errors.append('active workflow contains hardcoded pre-RC26 references')

if errors:
    print('\n'.join(errors))
    sys.exit(1)
print('workflow integrity: PASS')
