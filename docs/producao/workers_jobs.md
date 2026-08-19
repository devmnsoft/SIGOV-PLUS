# Workers e jobs

O projeto `Sigov.Worker` hospeda o processador Outbox e despacha handlers de webhook, integração, relatório, financeiro e suporte. `Workers__Outbox__Enabled=false` o desliga; habilite somente uma topologia aprovada. O loop aplica espera e cancellation token, e cada execução deve preservar tenant, idempotência e registrar sucesso/falha sem payload sensível.

Operação: acompanhe logs `Application=sigov`, backlog/tentativas e correlation id do evento; readiness do banco precede o start. Em repetição de falhas, desligue o worker, preserve mensagem e erro, corrija a causa e reative gradualmente—nunca apague a fila. Frequência/retry provêm da configuração e política do Outbox; mudanças exigem change registrado.
