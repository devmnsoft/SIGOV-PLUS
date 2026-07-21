# script_completop.sql Pós-RC 20

O `script_completop.sql` passou a ser gerado pelo manifest oficial, sem `\i`, sem comandos shell e sem seed demonstrativo. A baseline não cria usuário administrativo padrão nem senha administrativa padrão; o bootstrap inicial exige execução explícita dos scripts `create-initial-admin`.
