function Show($tag,$A,$B){ Write-Output ($tag + ' A=[' + $A + '] B=[' + $B + ']') }

# v1: cast+cast em uma linha com ';' (o pattern atual)
$c=@{ json=@{ id='id-v1'; numero='num-v1' } }
$A1=[string]$c.json.id; $B1=[string]$c.json.numero
Show 'v1 cast;cast      ' $A1 $B1

# v2: cast+cast em linhas separadas
$c=@{ json=@{ id='id-v2'; numero='num-v2' } }
$A2=[string]$c.json.id
$B2=[string]$c.json.numero
Show 'v2 cast/newline    ' $A2 $B2

# v3: sem cast, com ';'
$c=@{ json=@{ id='id-v3'; numero='num-v3' } }
$A3=$c.json.id; $B3=$c.json.numero
Show 'v3 plain;plain     ' $A3 $B3

# v4: primeiro sem cast, segundo com cast, ';'
$c=@{ json=@{ id='id-v4'; numero='num-v4' } }
$A4=$c.json.id; $B4=[string]$c.json.numero
Show 'v4 plain;cast      ' $A4 $B4

# v5: primeiro com cast, segundo sem cast, ';'
$c=@{ json=@{ id='id-v5'; numero='num-v5' } }
$A5=[string]$c.json.id; $B5=$c.json.numero
Show 'v5 cast;plain      ' $A5 $B5
