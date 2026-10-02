# Pegadinhas PowerShell 5.1 / ambiente dos gates SIGOV-PLUS

Consolidado da jornada (Bloco A/B), validado empiricamente nesta máquina
(Windows PowerShell 5.1.26100.9549, host UTC-3). Consultar antes de escrever
gate novo ou depurar assert misterioso.

## 1. Variáveis são CASE-INSENSITIVE (root cause do FAIL B4, run8)

`$C1` e `$c1` são a MESMA variável. Em atribuição composta:

```powershell
$C1=[string]$c1.json.id; $NUMC1=[string]$c1.json.numero
```

a primeira statement sobrescreve `$c1` (o objeto JSON) com a string do id;
a segunda lê `.numero` de uma string → em modo não-strict retorna `$null`
silenciosamente → `[string]$null` = `''`. Resultado: `NUMC1=[]` sem nenhum
erro, e o parse (AST) está perfeito — o bug é 100% semântico.

Regras:
- Atribuição única `$C2=[string]$c2.json.id` é SEGURA (RHS é avaliado antes
  do LHS ser escrito) — desde que nada leia `$c2` depois.
- Padrão seguro para extração dupla: snapshot antes.
  ```powershell
  $c1j=$c1.json
  $C1=[string]$c1j.id; $NUMC1=[string]$c1j.numero
  ```
- Detector: `python Temp/opencode/chk_collision.py` (linhas com `;`) e
  `chk_casefile.py` (pares de nomes que diferem só por case em todo o
  arquivo). Ambos deviam reportar 0 colisões ativas no gate_b/gate_a/lib/reset.

## 2. `.Count` em pipeline com exatamente 1 objeto → $null

PS 5.1 desta máquina: pipeline que produz exatamente 1 PSCustomObject
degenera; sem `Measure-Object`, `.Count` dá resultado inesperado.
Fix canônico antes de `-eq 1`: `(@($x) | Measure-Object).Count -eq 1`.

## 3. Logs dos gates saem em UTF-16LE

`Tee-Object` / redirecionamento `*>` gravam UTF-16LE (BOM FF FE). O read
normal vê "binary". Usar dumper python (`dump_runN.py`) ou `Select-String`.

## 4. Shell externo é PowerShell 5.1

- Sem `&&` — separar comandos em invocações ou usar `;`.
- Em `-Command "..."`, TODO `$` (inclusive `$_`) é expandido pelo shell
  externo ANTES do script rodar → sempre usar `-File script.ps1`.
- `.ps1` sem BOM é lido como ANSI (cp1252): acentos viram mojibake.
  Cosmético em comments/labels; o `gate_b.ps1` tem BOM UTF-8.

## 5. Splat de array com >=4 elementos no curl via cmd /c

Quebra argumentos no formato `<x>C:\...` em dois args. O JApi de
`jornada_lib.ps1` já contorna com `--data-binary @file` e header em arquivo.

## 6. Probes descartáveis: extensão .txt NÃO roda com `&`

`& 'arquivo.txt'` pode falhar silenciosamente (nenhum erro, nenhuma saída).
Sempre `.ps1` para micro-testes executados pelo PowerShell.
