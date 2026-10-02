$ErrorActionPreference = 'Stop'
$text = [System.IO.File]::ReadAllText('C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs', [System.Text.Encoding]::UTF8)
$markers = @(
  'System.Guid.Parse(r.GetProperty("id").GetString()!)',
  "select coalesce(resultado::text,'') from sigov.compras_empresarial_idempotencia where tenant_id=@t and operacao='REQUISICAO_ENVIAR'",
  'alcadaInsuficiente=politica is not null&&niveis.Count>0&&total>niveis.Max(n=>n.Item2)',
  'status,causa_bloqueio,regra_snapshot,ciclo',
  "jsonb_build_object('id',i.id,'ordem',i.ordem",
  'total,causa="SEM_POLITICA",us=',
  'topoAlcada=alcadaInsuficiente&&e.Item1==etapas[^1].Item1',
  'causa=topoAlcada?"ALCADA_INSUFICIENTE":"SEM_APROVADOR"',
  "causa=(string?)null,us=",
  "'APROVACAO_ALCADA_INSUFICIENTE','Aprovação com alçada insuficiente'",
  "'alcada_insuficiente',@alcadaInsuficiente",
  "values(@t,'REQUISICAO_ENVIAR',@key,@id,@hash)",
  "set resultado=jsonb_build_object('id',@id::text,'status','PENDENTE_APROVACAO','version',@v2,'ciclo',@ciclo)"
)
$allOk = $true
foreach ($m in $markers) {
  $c = ([regex]::Matches($text, [regex]::Escape($m))).Count
  $ok = ($c -ge 1)
  if (-not $ok) { $allOk = $false }
  Write-Output ("$(if ($ok) { "FOUND(" + $c + ")" } else { "MISSING" }) :: " + $m.Substring(0, [Math]::Min(70, $m.Length)))
}
Write-Output ("ALL_OK=" + $allOk)
