# Diagnóstico inicial Pós-RC 18

O estado inicial apontava ambiguidade entre services de aplicação e infraestrutura, service locator operacional, herança indevida de `ITarefaService` sobre `ITarefaRepository`, falso positivo possível no scanner de release e seed Agro dependente de `ON CONFLICT (chave)` sem constraint única garantida.

Ambiente local: a inspeção confirmou que o SDK `dotnet` não está instalado no container, portanto os checks .NET/Docker dependem do CI.
