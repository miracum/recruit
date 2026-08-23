# list-next feature plans

Working plans for features discussed but not yet built. Each section is a rough
design, not a spec — flesh out further before implementing.

## 1. Live presence indicator ("Alex is viewing this patient")

**Goal:** when two coordinators have the same patient open at once, each sees a
lightweight indicator that someone else is looking too, to cut down on duplicate
screening work. No Redis needed — reuse the Postgres-only pattern this app
already uses twice (`DataProtectionKeyContext` for the key ring, Hangfire's
`UsePostgreSqlStorage`) instead of adding a new infra dependency for what is a
low-frequency, latency-tolerant signal.

**Data model** — new table, e.g. `PatientViewer`:

- `PatientKey` (whatever `PatientDialog.razor` keys a patient on — likely
  `ResearchSubjectIdentifier` per `PatientListEntryDto`'s doc comment on that
  field)
- `UserId`, `DisplayName`
- `ReplicaId`
- `LastSeenAt`

**Flow:**

- `PatientDialog.razor` upserts a row on open, heartbeats every ~10-15s while
  open, deletes on close/`IAsyncDisposable`.
- A Hangfire recurring job (same clustered-poll mechanism
  `Services/Notify/NotificationDetectorService.cs` already uses — "Hangfire
  guarantees exactly one list-next replica fires per tick") runs every few
  seconds, not the notification poller's ~60s: reads rows for currently-open
  patients, diffs against the last snapshot per replica, and pushes changes to
  subscribed components via a local in-process pub/sub → `InvokeAsync(StateHasChanged)`.
- TTL cleanup: a row older than ~30s with no heartbeat is treated as stale
  (handles crashed tabs/network drops), same spirit as `PollCursor`'s
  concurrency-token approach.

**Alternative considered:** Postgres `LISTEN/NOTIFY` for push instead of
poll+table — sub-second latency, but a long-lived connection to manage per
replica and no replay if a notify is dropped. Not worth it for a "someone else
is looking" indicator; start with poll+table, only revisit if it feels laggy.

**Pre-existing gap this depends on:** `Program.cs:178-180` says ingress sticky
sessions are "documented in the README," but no such doc exists anywhere in the
repo (checked `README.md`, `charts/recruit/README.md`, `ingress.yaml`, no
affinity annotation wired up). Blazor Server already needs stable
circuit-to-replica routing for reconnects to work well at `replicaCount > 1`,
independent of presence — worth closing first since presence makes multi-replica
correctness more visible to users, not less.

## 2. Patient/subject assignment (ownership) — implemented

Implemented per-trial ownership as planned below: `PatientAssignment` entity/migration,
`PatientAssignmentService` (`AssignToMeAsync`/`UnassignAsync`/`GetAssignmentsAsync`), an assignee
avatar column in `PatientList.razor`, and "Assign to me"/"Unassign" in `PatientDialog.razor`
(works from both `PatientList.razor` and the cross-trial `Patients.razor`, since both share that
dialog). Not done yet: an assignee column on `Patients.razor` itself (the cross-trial page never
bulk-loads assignments the way `PatientList.razor` does, so its table has no assignee indicator,
though opening a patient's dialog from there still shows/edits the correct assignment) and a "My
patients" filter toggle. Original plan below, kept for reference.

**Goal:** let a coordinator claim a candidate ("assigned to me") so multiple
screeners working the same trial don't duplicate effort — complements #1
(presence is ephemeral/"looking now", assignment is durable/"responsible for").

**Data model** — new entity alongside the existing `Data/Entities/*` (e.g.
`ScreeningNote`, `TrialAccessGrant`): `PatientAssignment` keyed the same way
screening notes are — `ResearchSubjectIdentifier`, not `ResearchSubjectId` (see
the doc comment on `PatientListEntryDto.ResearchSubjectIdentifier` explaining
why notes use the business identifier, not the FHIR resource id). Columns:
`ResearchSubjectIdentifier`, `AssignedToUserId`, `AssignedToDisplayName`,
`AssignedAt`, `AssignedByUserId`.

**Service:** a small `PatientAssignmentService`, same shape as
`ScreeningNoteService` — `GetAsync`, `AssignAsync(subjectIdentifier, user)`,
`UnassignAsync`.

**UI:**

- `PatientListEntryDto` gets an `AssignedToDisplayName` field, populated the
  same lazy/on-demand way `LastKnownLocation` already is.
- An assignee avatar/badge column in `PatientList.razor`'s `BbDataTable`
  (`BbAvatar`/`BbAvatarFallback`, matching the assignee-avatar pattern used
  throughout the BlazorBlueprint `app-tasks` blueprint's task rows).
- "Assign to me" / "Unassign" action in `PatientDialog.razor`.
- Optional filter: "My patients" toggle on `PatientList.razor` — this can reuse
  whatever filter control gets built for the plain status-filter gap noted
  separately (today the table has sorting/paging only, no filter of any kind).

**Open question:** should assignment be per-trial-per-patient (a candidate can
be assigned to different coordinators in different trials they're a candidate
for) or per-patient globally? Given `PatientListEntryDto.ResearchSubjectIdentifier`
is already trial-scoped (one `ResearchSubject` per trial-candidacy), per-trial
is the natural fit and requires no new join key.

## 3. Kanban board view for a trial's patient list

**Goal:** an alternative to `PatientList.razor`'s flat `BbDataTable`, columns by
`ResearchSubject.status` (candidate → screening → eligible → on-study /
ineligible / withdrawn — the same status vocabulary `StatusBadge.razor` and
`PatientListEntryDto.Status` already use), so a coordinator can see and move
candidates through the funnel visually. Modeled on BlazorBlueprint's
`app-tasks` blueprint board view (`BbTabs` list/board toggle, priority columns
as horizontally-scrolling flex columns of `BbCard`s) — that blueprint's board is
static markup with no drag-and-drop wired in (BlazorBlueprint has no
drag/sortable primitive), so column layout is a straight port but movement
needs its own mechanism.

**Structure, following the blueprint:**

- `BbTabs` at the top of `PatientList.razor` alongside the existing
  criteria/summary header: `List` (today's `BbDataTable`) / `Board`.
- Board: one flex column per `ResearchSubject.status` value, header showing
  status label + count (reuse `StatusBadge`'s status→label mapping), each card
  showing name/MRN, age/gender, assignee avatar (once #2 exists), "new" badge
  (`PatientListEntryDto.IsNew`), same click-to-open-`PatientDialog` behavior as
  today's table rows.

**Moving a card between columns — two mechanisms, both worth building:**

- **Accessible fallback (build first):** a "Move to..." dropdown on each card,
  same affordance as the `app-tasks` blueprint's `BbDropdownMenuItem` with the
  arrow-right icon — lists the valid next statuses and calls the same
  `ScreeningListService`/`ResearchSubjectService` status-update path
  `PatientDialog.razor` already uses today. Zero new client-side machinery,
  works with keyboard/screen readers, ships fast.
- **Drag-and-drop (stretch):** native HTML5 drag events (`@ondragstart`,
  `@ondragover`, `@ondrop`) bound directly in Razor — no JS interop or extra
  package needed for a same-page, same-circuit drag. Skip a JS drag library;
  Blazor Server's per-card `@onclick`-style event bindings handle this natively
  and keep the whole feature server-rendered.

**Constraint to design around:** not every status transition is actually valid
today — check what `ResearchSubjectService`/`ScreeningListService` currently
allow (e.g. is "on-study" only reachable from "eligible"?) before letting the
board offer moves the backend would reject; the "Move to..." menu should only
list transitions the service accepts.

## 4. Stalled recruitment alert

Two distinct features hide under this name — the infra for one already exists
half-wired, the other doesn't exist at all:

**4a. Per-lead "stalled" highlight (infra already half-built, just needs code).**
`charts/recruit/values.yaml:824-828` already documents and sets
`notifications.stalledLeadWindowDays: 14` ("a pending recommendation is
highlighted as stalled if untouched for this many days"), and the Helm
deployment template already passes it through as
`Notifications__StalledLeadWindowDays` (`charts/recruit/templates/list-next/deployment.yaml:164`).
But `Options/AppOptions.cs`'s `NotificationOptions` has no
`StalledLeadWindowDays` property, and nothing in the codebase reads that env
var — it's currently a no-op config value. To close the loop:

- Add `public int StalledLeadWindowDays { get; set; } = 14;` to
  `NotificationOptions` in `Options/AppOptions.cs`.
- Add `PatientListEntryDto.IsStalled(int windowDays)`, mirroring the existing
  `IsNew(int windowDays)` — true when `Status == "pending"` (or whatever the
  in-progress status values are) and `LastUpdated` is older than the window.
- Surface it in `PatientList.razor`/board cards the same way `IsNew` renders
  today's "New" `BbBadge`, e.g. an outline/warning-variant "Stalled" badge.

**4b. Proactive "this study has gone quiet" notification (net-new).** Different
problem: not a single stalled lead, but a whole study receiving zero new
candidates for N days — in practice this is more often a sign the study's
SQL-on-FHIR criterion or the query module broke than that recruitment is
genuinely dry, so it's worth alerting on independently of 4a. This fits the
existing notification pipeline (`Services/Notify/NotificationDetectorService.cs`,
`NotificationSenderService`, `INotificationChannel`) as a new detection rule
alongside the existing per-patient diffing: track last-new-candidate timestamp
per `List`, and if it exceeds a (new, separate) config threshold, emit a
`NotificationEvent` of a new kind ("study stalled") that renders through the
same digest/email path `NotificationDigest.mjml.liquid` already handles for
per-patient events.

**Note:** `INotificationChannel` currently has exactly one implementation
(`EmailNotificationChannel`) despite being an interface built for more — a
Slack/Teams webhook channel would be a natural second implementation to pair
with 4b, since "the study went quiet" is the kind of alert a team channel
notices faster than an inbox does.
