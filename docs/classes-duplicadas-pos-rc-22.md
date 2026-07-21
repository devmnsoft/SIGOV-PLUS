# Classes duplicadas Pós-RC 22

## MobileCampoController

- Tipo: controller MVC duplicado no namespace `Sigov.Web.Controllers`.
- Arquivos encontrados: `src/Sigov.Web/Controllers/OperationalTransversalController.cs` e `src/Sigov.Web/Controllers/MobileCampoController.cs`.
- Rotas conflitantes: `/MobileCampo`, `/MobileCampo/Roteiros`, `/MobileCampo/Coletas`, `/MobileCampo/Evidencias`, `/MobileCampo/Sincronizacao`, `/MobileCampo/Dispositivos`, `/MobileCampo/Conflitos`.
- Decisão: manter a implementação operacional transversal, pois usa `MobileCampoService` e compartilha a view operacional real `Views/Operational/Hub.cshtml` com o Núcleo Operacional.
- Arquivo mantido: `src/Sigov.Web/Controllers/OperationalTransversalController.cs`.
- Código removido: classe `MobileCampoController` duplicada de `src/Sigov.Web/Controllers/MobileCampoController.cs`; os controllers `MobileController`, `CampoController` e `OfflineController` foram preservados.
