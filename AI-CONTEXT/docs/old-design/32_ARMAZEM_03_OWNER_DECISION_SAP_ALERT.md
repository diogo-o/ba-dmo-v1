# Resolved owner decision — stored SAP utilisation alert

- SAP utilisation is a manual fact entered in Ferramentas; the application never calculates it.
- Armazém may consume the stored utilisation to display the appropriate alert when a tool enters storage.
- Armazém does not own or edit the utilisation record.
- Future automatic SAP integration is out of scope.

## REFINED BY LATER OWNER CLARIFICATION Q2 + Q4 (Boquilhas/Armazém — supersedes the literal reading of the line above)

- **Q2 (OWNER-CONFIRMED):** `% utilização` é **sempre manual**; o sistema nunca a calcula, incrementa, deriva nem atualiza automaticamente; quando a ferramenta sai de Produção e entra no Armazém, o sistema apresenta **apenas um reminder/alarme para atualizar `% utilização`** — o reminder não calcula, não infere, não modifica o valor e não bloqueia.
- **Q4 (OWNER-CONFIRMED):** o registo BQ/Lote **existente** é **consultado/mantido a partir do Armazém**, pelo perfil **RESPONSÁVEL**, nas **características funcionalmente confirmadas como editáveis** (a Q4 não torna automaticamente todos os campos editáveis).
- **Consequência combinada Q2+Q4:** "Armazém does not own or edit the utilisation record" deve ser lido como: o Armazém **não é dono** de `% utilização` (Ferramentas permanece o domínio master) e **não calcula/atualiza automaticamente** o valor; porém, sendo o Armazém a **superfície operacional de manutenção do registo existente**, a **atualização manual** de `% utilização` **pode ser realizada ali pelo Responsável** onde a característica estiver exposta como editável. **Nenhuma automatização de escrita;** nenhuma transferência de posse.

