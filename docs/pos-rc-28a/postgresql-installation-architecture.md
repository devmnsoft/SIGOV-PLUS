# Arquitetura de instalação PostgreSQL

A instalação limpa deve ser derivada exclusivamente de um install manifest canônico, enquanto upgrades continuam usando o manifest histórico. O baseline não pode conter tenant, usuário, credencial, dado demonstrativo ou objetos PlantãoPro. O script SQL puro é distinto do orquestrador psql com roles e parâmetros.
