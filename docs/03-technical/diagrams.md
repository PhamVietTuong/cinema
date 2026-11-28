# Diagrams

> Purpose: Four views of the system as Mermaid diagrams: system context, data model (ERD), the booking-to-VNPay-IPN sequence, and deployment.
> Sources: `docker-compose.yml`, `.github/workflows/ci.yml`, `cinemabe/sql/create_db.sql`, `cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost/{Program.cs,Controllers/PaymentController.cs,Hubs/BookingHub.cs,Services/PendingBookingReaper.cs}`, `cinemabe/Cinema/2-Business/Cinema.Business/{Managers/BookingManager.cs,Payments/VnPayGateway.cs}`, `cinemabe/Cinema/3-Data/Cinema.Data/Stores/InvoiceStore.cs`, `docs/03-technical/{architecture,integrations,database-schema}.md`
> Last reviewed: 2026-11-14

A draw.io set (13 pages) for Word or slides is in [`drawio/cinema.drawio`](drawio/cinema.drawio); open it in app.diagrams.net and export PNG/SVG. Pages 1-7 are technical (architecture, ERD, booking swimlane, VNPay sequence, state machines, use cases); pages 8-13 are business (pricing, promotions and loyalty, counter sale and gate scan, showtime scheduling, sign-in and 2FA, business model canvas). The canvas marks the cells the docs cannot fill (revenue model, costs, market size) as owner input.

The layer dependency graph (a fifth view) is in [`architecture.md`](architecture.md) section 2. Each diagram below lists what it was checked against, so a reader can re-verify it.

## 1. System context

Who uses the system, and which outside services it talks to. Ports are the development defaults from `AGENTS.md`.

```mermaid
flowchart LR
    Customer(["Customer"])
    Staff(["Theater staff"])
    Admin(["Admin"])

    subgraph Browsers["Angular 21 apps (browser)"]
        UserApp["CinemaUser :4202"]
        StaffApp["CinemaStaff :4203"]
        AdminApp["CinemaAdmin :4201"]
    end

    subgraph Backend["Cinema Web API :5102 (.NET 9)"]
        Api["CinemaController, IdentityController, PaymentController"]
        Hubs["SignalR hubs: /hubs/booking, /hubs/staff"]
        Jobs["Background jobs: PendingBookingReaper, ShowtimeReminderService"]
    end

    DB[("SQL Server")]
    VNPay["VNPay (payment gateway)"]
    OAuth["Google and Facebook sign-in"]
    OMDb["OMDb (movie ratings)"]
    Smtp["SMTP (email)"]
    Twilio["Twilio (SMS)"]

    Customer --> UserApp
    Staff --> StaffApp
    Admin --> AdminApp
    UserApp -->|HTTPS JSON, JWT| Api
    StaffApp -->|HTTPS JSON, JWT| Api
    AdminApp -->|HTTPS JSON, JWT| Api
    UserApp <-->|WebSocket, token in query string| Hubs
    StaffApp <-->|WebSocket| Hubs
    Api --> DB
    Jobs --> DB
    Api -->|verify ID token| OAuth
    Api -->|rating lookup, 3 s timeout| OMDb
    Api -->|confirmation, reset, 2FA, reminders| Smtp
    Api -->|2FA code, confirmation| Twilio
    Api -->|signed payment URL| VNPay
    VNPay -->|signed IPN, server to server| Api
    UserApp -->|browser redirect to pay| VNPay
```

Checked against: the three apps and ports in `AGENTS.md`; the controllers and hubs in `architecture.md` sections 4 and 5; gateway, OAuth, OMDb, SMTP and SMS integrations in `integrations.md`; the 3 second OMDb timeout at `DependencyInjection.cs:25`; the two hosted services in `Program.cs` and `architecture.md` section 8. SMTP and Twilio are used only when configured; otherwise a log-only implementation is registered (`integrations.md` section 6).

## 2. Data model (ERD)

Core entities and the foreign keys declared in `cinemabe/sql/create_db.sql` (`create_db.sql` creates 42 tables). Not drawn: `ComboItem` (two foreign keys to `FoodAndDrink`: the combo and its component) and `StockMovement` (a foreign key to `FoodAndDrink`); `Holiday`, `News`, `Template`, `SplashBanner` and `__EFMigrationsHistory` have no foreign keys; `LoyaltySetting` (`TheaterId`), `ReminderLog` (`UserId`, `ShowTimeId`), `Incident` (theater, room, seat, showtime and user ids) and `AuditLog` (by design) hold plain id columns with no foreign key. Primary keys are `Guid`; `InvoiceTicket` has a composite key.

```mermaid
erDiagram
    Theater ||--o{ Room : has
    Theater ||--o{ RoomType : defines
    Theater ||--o{ SeatType : defines
    Theater ||--o{ PatronCategory : defines
    Theater ||--o{ TimeSlot : defines
    Theater ||--o{ FoodAndDrink : stocks
    Theater |o--o{ User : "staff of"
    RoomType ||--o{ Room : classifies
    Room ||--o{ Seat : contains
    SeatType ||--o{ PatronCategory : "priced as"
    RoomType ||--o{ RoomTypePatronCategoryPrice : prices
    PatronCategory ||--o{ RoomTypePatronCategoryPrice : "priced in"
    TimeSlot ||--o{ TicketPrice : scopes
    RoomType ||--o{ TicketPrice : scopes

    AgeRestriction ||--o{ Movie : rates
    Movie ||--o{ ShowTime : "is screened as"
    ShowTime ||--o{ ShowTimeRoom : "is held in"
    Room ||--o{ ShowTimeRoom : hosts
    Movie ||--o{ MovieCredit : credits
    Person ||--o{ MovieCredit : "appears in"
    Movie ||--o{ MovieTypeDetail : tagged
    MovieType ||--o{ MovieTypeDetail : tags
    Movie ||--o{ Comment : receives
    Movie ||--o{ Evaluation : receives

    UserType ||--o{ User : "role of"
    MemberShip ||--o{ User : "tier of"
    User |o--o{ Invoice : "places (customer optional)"
    Theater |o--o{ Invoice : "sold at"
    PromotionProgram |o--o{ Invoice : "applied to"
    User ||--o{ Comment : writes
    User ||--o{ Evaluation : writes
    Comment |o--o{ Comment : "reply to"

    Invoice ||--o{ InvoiceTicket : contains
    Seat ||--o{ InvoiceTicket : "sold as"
    ShowTimeRoom ||--o{ InvoiceTicket : "sold for"
    Invoice ||--o{ InvoicePayment : "paid by"
    Invoice ||--o{ InvoiceFoodAndDrink : includes
    FoodAndDrink ||--o{ InvoiceFoodAndDrink : "sold as"

    PromotionCampaign |o--o{ PromotionProgram : groups
    Movie |o--o{ PromotionProgram : "limited to"
    MemberShip |o--o{ PromotionProgram : "minimum tier"
    PromotionProgram ||--o{ PromotionProgramPrice : prices
    PromotionProgram ||--o{ PromotionProgramSchedule : schedules

    Invoice {
        Guid Id PK
        Guid UserId FK "nullable: walk-in counter sales have no customer"
        int Status "Pending, Paid, Cancelled, Failed"
        string PaymentMethod
        string PaymentReference
    }
    InvoiceTicket {
        Guid InvoiceId PK
        Guid ShowTimeId PK
        Guid RoomId PK
        Guid SeatId PK
        bool IsActive "unique per seat while 1"
        bool IsUsed
    }
```

Checked against: every relationship line is a `FOREIGN KEY` in `create_db.sql`, inline in a `CREATE TABLE` or added by `ALTER TABLE` (extracted by script on 2026-11-12); `FK_Invoice_User_UserId` is inline at `create_db.sql:512`, and the `Theater` and `PromotionProgram` ones are added by `ALTER TABLE` at `create_db.sql:818-827`. `Invoice.UserId` is altered to nullable at `create_db.sql:809`. One guarantee worth knowing: the unique filtered index `IX_InvoiceTicket_ActiveSeat` on `(ShowTimeId, RoomId, SeatId) WHERE IsActive = 1` (`create_db.sql:700`) is what stops double booking across server instances. Field-level detail is in [`database-schema.md`](database-schema.md).

## 3. Booking, VNPay IPN and confirmation

The money path. The IPN (the signed server-to-server call) is the only thing that marks an invoice Paid; the browser redirect only shows the result.

```mermaid
sequenceDiagram
    autonumber
    actor C as Customer (CinemaUser)
    participant H as BookingHub (SignalR)
    participant A as API (PaymentController)
    participant B as BookingManager
    participant D as SQL Server
    participant V as VNPay
    participant N as Email and SMS

    C->>H: JoinRoom, LockSeat (advisory, 5 min, in memory)
    H-->>C: SeatLocked broadcast to the room

    C->>A: CreateBooking (seats, code, points)
    A->>B: CreateBookingAsync
    B->>D: begin transaction, insert Invoice (Pending) and InvoiceTicket rows
    alt a seat was taken by someone else
        D-->>B: unique index IX_InvoiceTicket_ActiveSeat violated
        B-->>A: rolled back, "just booked by someone else"
        A-->>C: error shown to the customer
    else seats are free
        B->>D: commit
        B-->>H: SeatBooked broadcast
        A-->>C: Pending invoice
    end

    C->>A: InitiatePayment (invoice id)
    A->>B: InitiatePaymentAsync (owner and Pending checked)
    B->>D: store PaymentMethod = VNPay
    B-->>A: signed pay URL (TxnRef per attempt, expires in 15 min)
    A-->>C: redirect URL
    C->>V: pays on the VNPay page

    V->>A: IPN api/Payment/PaymentCallback?provider=VNPay (anonymous)
    A->>B: HandlePaymentCallbackAsync
    B->>B: verify HMAC-SHA512, order, amount x100 equals FinalAmount
    alt bad signature, unknown order or wrong amount
        A-->>V: 200 {"RspCode":"97" or "01" or "04"}
    else signed IPN reports an unsuccessful payment
        A-->>V: 200 {"RspCode":"00"} (acknowledged, invoice stays Pending, nothing written)
    else valid
        B->>D: begin transaction, UPDATE Invoice SET Status = Paid WHERE Status = Pending
        alt claim lost (duplicate IPN, or the reaper cancelled it first)
            B->>D: roll back
            A-->>V: 200 {"RspCode":"02"}
        else claim won
            B->>D: payment row, loyalty points, promotion use count, commit
            B-->>N: confirmation email and SMS (best effort)
            A-->>V: 200 {"RspCode":"00","Message":"Confirm Success"}
        end
    end

    V-->>C: browser redirect to the return page
    loop every 2 s, up to 15 times (about 30 s)
        C->>A: GetInvoice
        A-->>C: Paid, Pending ("payment pending") or Cancelled
    end

    Note over B,D: An unsuccessful payment never sets Failed, only the admin status override can. PendingBookingReaper runs every minute and cancels Pending invoices older than 15 min (30 min once payment was started), freeing seats and restoring points
```

Checked against: `PaymentController.cs:74` (CreateBooking), `:297`/`:313` (GetInvoice family), `:166` (IPN reply built by `VnPayGateway.BuildIpnResponse`); `BookingManager.cs:1086` (atomic Pending to Paid claim) and `:1831`, `:1856` (reaper claim); the codes 00/01/02/04/97/99 in `business-rules.md` section 3; `BookingHub.cs:22-49` (`SeatLocked`/`SeatUnlocked`; `SeatBooked` is sent from `BookingManager` through `ISeatNotificationService`); `payment-return.component.ts:18-19` (2 s, 15 attempts); `VnPayGateway.cs:74` (15 minute expiry); `PendingBookingReaper.cs:12-13`. The unsuccessful-payment branch is `BookingManager.cs:1059-1061` (`PaymentFailedAcknowledged`); `InvoiceStatus.Failed` is reached only through `InvoiceAdminManager.UpdateStatusAsync` (`InvoiceAdminManager.cs:42-62`). Unresolved gateway outcomes (for example a payment confirmed after the invoice was cancelled) answer `RspCode` 02 and produce no ticket (`business-rules.md` section 2).

## 3a. Two customers, one seat (concurrent booking)

Three layers stop a double sale. Only the third holds when the API runs as more than one instance.

```mermaid
sequenceDiagram
    autonumber
    actor A as Customer A
    actor B as Customer B
    participant H as BookingHub (advisory lock, in memory)
    participant G as BookingGate (SemaphoreSlim per showtime + room)
    participant M as BookingManager
    participant D as SQL Server

    Note over A,D: Layer 1 - advisory lock (5 min, UX only)
    A->>H: LockSeat A1
    H-->>B: SeatLocked A1 (B's seat map shows it held)

    Note over A,D: Layer 2 - gate serializes "read booked, validate, insert" in one process
    par both press Pay at the same time
        A->>G: CreateBooking {A1, A2}
    and
        B->>G: CreateBooking {A2, A3}
    end
    G->>M: A enters (B waits on the semaphore)
    M->>D: begin transaction, read booked seats (none), insert tickets A1 + A2, commit
    G->>M: A leaves, B enters
    M->>D: read booked seats (A2 is booked)
    M-->>B: InvalidOperationException "Seat ... is already booked." (nothing written, A3 stays free)

    Note over A,D: Layer 3 - DB unique filtered index, the real guarantee across instances
    opt B was served by another API instance (gate does not help)
        M->>D: insert ticket A2 for B
        D-->>M: IX_InvoiceTicket_ActiveSeat violated
        M->>D: rollback (all-or-nothing)
        M-->>B: "One or more selected seats were just booked by someone else."
    end
```

| Layer | Where | Survives several instances? | What it protects |
|---|---|---|---|
| Advisory lock | `_lockedSeats` in `BookingManager`, broadcast by `BookingHub` | No | Tells other viewers a seat is being chosen; a UX hint, not a guarantee |
| Booking gate | `_bookingGates`, `BookingManager.BookingGate` | No | Stops two requests in one process both passing the "seat free" check |
| Unique filtered index | `IX_InvoiceTicket_ActiveSeat` on `(ShowTimeId, RoomId, SeatId) WHERE IsActive = 1` | Yes | A seat can be active on one ticket only, whichever instance writes it |

All-or-nothing: the check and the ticket inserts share one transaction, so a booking that loses on any seat is rolled back whole and keeps none of its other seats.

Known limit: with several API instances the loser of a race only finds out when the insert is rejected, not when picking the seat, because the advisory lock is not shared. Moving the hold into a DB table with an expiry would close that gap; it has not been built.

Checked against: `BookingManager.cs:37-82` (gate), `:197-285` (`CreateBookingAsync`, including the `SeatUnavailableException` catch), `:590-595` (already-booked check), `create_db.sql:721` (index). Tests: `BookingServiceTests.CreateBookingAsync_TwoConcurrentBookingsForSameSeat_OnlyOneSucceeds` and `CreateBookingAsync_ConcurrentBookingsWithOverlappingSeatSets_AreAllOrNothing`. They use a mocked store, so they cover layer 2 only; layer 3 has no integration test yet.

## 4. Deployment

What runs where today. There is no release or deploy pipeline: CI builds and tests only (`docs/05-process/deployment.md`). The compose file is a local demo stack.

```mermaid
flowchart TB
    subgraph Dev["Developer machine"]
        Browser["Browser"]
        Ng["ng serve: CinemaAdmin 4201, CinemaUser 4202, CinemaStaff 4203"]
        Ngrok["ngrok http 5102 (dev only, for the VNPay IPN)"]
    end

    subgraph Compose["docker compose"]
        Init["db-init: runs create_db, upgrade_db, insert_db once, then exits"]
        Db[("db: SQL Server 2022, port 1433, volume cinema-mssql")]
        Api["api: built from cinemabe/Dockerfile, host port 5102 to container 8080, volume cinema-logs, Payments__EnableSandbox=true (demo only)"]
    end

    subgraph GH["GitHub Actions (ci.yml, on push and pull request to master)"]
        J1["backend: build, analyzer ratchet, tests, 60% coverage gate"]
        J2["secrets: gitleaks scan"]
        J3["docker: image build only, no push"]
        J4["frontend: lint ratchet, unit tests, production builds"]
        J5["e2e: SQL Server service, API and apps, Playwright, Payments__EnableSandbox=true"]
    end

    VNPaySandbox["VNPay sandbox"]

    Browser --> Ng
    Ng -->|HTTP and WebSocket| Api
    Init -->|schema and seed| Db
    Db -.->|healthy before init| Init
    Api --> Db
    VNPaySandbox -->|IPN to the public ngrok URL| Ngrok
    Ngrok --> Api
    Browser -->|redirect to pay| VNPaySandbox
    VNPaySandbox -->|redirect back to the return page| Browser
```

Checked against: `docker-compose.yml` (services `db`, `db-init`, `api`, ports `1433` and `5102:8080`, volumes `cinema-mssql`, `cinema-logs`, `db-init` starts after `db` is healthy and the `api` after `db-init` completes); `ci.yml` job names and triggers (lines 1-9 and the `backend`, `secrets`, `docker`, `frontend`, `e2e` jobs); `AGENTS.md` for the three dev ports and the ngrok requirement. The CI `e2e` job sets `Payments__EnableSandbox` at `ci.yml:198` and the compose file sets it too (see ADR-0007). The compose `api` service runs with `ASPNETCORE_ENVIRONMENT=Production` and `Payments__EnableSandbox=true`, which is for local demos only. Student input needed: where the system is meant to be hosted for the final demonstration, if anywhere beyond a laptop; no hosting target is recorded in the repository.
