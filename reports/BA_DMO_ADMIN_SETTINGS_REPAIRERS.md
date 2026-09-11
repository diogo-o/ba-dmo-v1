# BA-DMO — Admin Settings: Repairers

> **Status:** OWNER-DIRECTED EXTENSION OF `R-046` / `R-047` — NOT IMPLEMENTED
>
> This file does not create a separate competing authority. It records a concrete detail of the already-authoritative decision that module configuration belongs under **Admin → Configurações**.

## Core decision

Repairer master data is configuration and therefore belongs in **Admin → Configurações**, not in the operational repair screens.

The operational modules consume the configured repairers.

## Repairer records

Admin must allow creation/maintenance of repairer records (“fichas dos reparadores”) that can later be selected by operational workflows.

Do not create a second ad-hoc repairer identity inside Armazém, Boquilhas or repair records. Operational records should reference the configured repairer identity.

## CM / MF external repair

For CM and MF, the repairer is selected/associated at the moment the lot/tool movement is sent for external repair.

Conceptually:

```text
Admin → Configurações → Reparadores
              ↓
       canonical repairer
              ↓
Armazém → saída/reparação CM/MF
              ↓
repair/movement record references repairer_id
```

The same repairer can therefore be used for different CM/MF movements without encoding a permanent machine/line ownership rule on the repairer.

## Boquilhas

Boquilhas require additional repairer configuration by line/machine context.

A BQ repairer may work with multiple lines, for example:

```text
Repairer A
- B1
- C2
- C3
```

Therefore the Boquilhas settings must allow associating **zero, one or multiple lines** to a configured repairer as applicable.

This is configuration used to help the BQ module present/filter the appropriate repairers for the selected line/context.

Do not model this as a single line field.

Do not duplicate the repairer record per line.

Conceptually:

```text
Repairer
- repairer_id
- name / canonical identity
- active/status fields as required

RepairerBQLine
- repairer_id
- line_id
```

or an equivalent normalized relation.

## Operational use in Boquilhas

When a BQ repair record is created in production context, the record should be able to preserve at least:

- repairer;
- production;
- BQ/tool identity;
- reference;
- lot;
- line/machine context;
- dates/state/notes required by the BQ repair workflow.

The repairer options shown to the user may be filtered by the line associations configured in Admin.

This filtering is a UI/context aid. It must not silently invent industrial truth beyond the explicit configuration entered by the user.

## Required distinction

- **Admin → Configurações:** create/edit repairer master data and, for Boquilhas, associate the repairer with the lines they service.
- **Armazém / CM-MF repair operation:** select the repairer each time the CM/MF repair movement is created.
- **Boquilhas operation:** select/use a configured repairer in the BQ workflow, with line-aware filtering from the Admin configuration.

## Acceptance criteria

1. Repairer identity is configured once and reused.
2. CM/MF movement records reference the selected configured repairer.
3. A BQ repairer can be associated with multiple lines.
4. The same BQ repairer does not need duplicate records for B1/C2/C3.
5. Boquilhas can filter/select repairers based on configured line associations.
6. Historical repair records remain tied to the exact repairer selected at that time.
7. Operational pages do not become repairer-master-data editors.
