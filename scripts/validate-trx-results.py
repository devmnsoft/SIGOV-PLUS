#!/usr/bin/env python3
import glob, json, sys, xml.etree.ElementTree as ET
from pathlib import Path
paths=[]
for arg in sys.argv[1:]: paths += glob.glob(arg, recursive=True)
if not paths:
    print('No TRX files found')
    sys.exit(1)
rows=[]; failed=False
for path in sorted(set(paths)):
    root=ET.parse(path).getroot()
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    counters=root.find('.//t:Counters', ns)
    if counters is None:
        print(f'{path}: missing counters'); failed=True; continue
    total=int(counters.attrib.get('total','0')); passed=int(counters.attrib.get('passed','0')); fail=int(counters.attrib.get('failed','0')); skipped=int(counters.attrib.get('notExecuted','0'))
    duration=root.attrib.get('duration','')
    project=Path(path).parent.name
    rows.append({'project':project,'total':total,'passed':passed,'failed':fail,'skipped':skipped,'duration':duration,'trx':path})
    if total <= 0 or fail != 0 or skipped != 0 or passed != total:
        print(f'{path}: invalid counters total={total} passed={passed} failed={fail} skipped={skipped}')
        failed=True
Path('artifacts').mkdir(exist_ok=True)
Path('artifacts/test-summary.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
md=['| projeto | total | passed | failed | skipped | duração | TRX |','|---|---:|---:|---:|---:|---|---|']
for r in rows: md.append(f"| {r['project']} | {r['total']} | {r['passed']} | {r['failed']} | {r['skipped']} | {r['duration']} | {r['trx']} |")
Path('artifacts/test-summary.md').write_text('\n'.join(md)+'\n',encoding='utf-8')
print(f'TRX validation: PASS ({len(rows)} file(s))')
sys.exit(1 if failed else 0)
