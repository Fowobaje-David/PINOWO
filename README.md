# Pínówó - Crypto Expense Splitter

Pínówó is a group expense-splitting app where balances are **denominated in crypto
value** (Bitcoin and a USDT stablecoin) instead of fiat. Groups record shared
expenses, the system computes who owes whom, and balances are reconciled to a
common **USD-equivalent** reference using live exchange rates - with real-time
updates and a documented REST API.

---

## Scope decision: crypto as the *unit of account* (please read)

This MVP treats BTC and the stablecoin as the **unit of account, not custodied funds**.
Expenses are entered and balances are calculated in crypto value using live CoinGecko
rates, and settlement is recorded as *"marked as settled"* - **no real wallets, private
keys, or on-chain transactions are involved**. Building real BTC custody, transaction
signing, and broadcasting safely is out of scope for this delivery window; the value of
the system is in a correct, well-tested **balance engine**, a real REST API, and a
real-time UI. This is an intentional cut, not an oversight (see PRD Section 3, Non-Goals).

### Known tradeoff: net balances are shown in USD-equivalent

Because a single group can hold a **mix** of BTC and stablecoin debts, the **net** balance
between two people is expressed in **USD-equivalent** - you cannot net "0.001 BTC owed"
against "$50 USDT owed" into one crypto number without a common reference. The original
crypto currency **is** preserved and displayed at the **expense and share level** (each
expense shows its BTC/USDT amount alongside its USD-at-entry value); only the *netted*
who-owes-whom figure is USD. This is a deliberate, documented choice.

To keep historical balances stable, each expense snapshots its USD value **at entry time**
(`AmountInUsdAtEntry`); the balance engine derives every share's USD value proportionally
from that snapshot, so past balances don't move when the market moves.

---

## Tech stack

| Layer | Choice |
|---|---|
| Backend | ASP.NET Core MVC (**.NET 10**) - one project serving both the MVC UI and the REST API |
| Auth | ASP.NET Core Identity (`User : IdentityUser<int>` - Identity owns password hashing) |
| Data | EF Core (Code-First) → SQL Server (LocalDB), with migrations |
| Real-time | SignalR (`/hubs/balances`) for live balance updates |
| Rates | CoinGecko public API, cached in `ExchangeRateSnapshot` (5-minute TTL, offline fallback) |
| Tests | xUnit + EF Core InMemory (`Pinowo.Tests`) |

> **Note on .NET version:** the PRD specified .NET 8, but the build machine only has the
> .NET 10 SDK installed, so the project targets `net10.0`. ASP.NET Core MVC + EF Core +
> Identity are materially identical for this app's scope.

---

## Features (PRD Section 2 goals)

- Register / log in / log out (ASP.NET Core Identity).
- Create groups and add existing users as members.
- Add expenses in **BTC** or **USDT**, choose the payer, **equal split** across all members.
- Automatic **net pairwise balances** ("who owes whom") per group.
- Balances shown in crypto (per expense) **and** USD-equivalent (net), using a rate ≤ 5 min old.
- **Real-time** balance updates via SignalR - no page refresh when an expense is added.
- Documented **REST API** for users, groups, expenses, balances, and rates.
- Mark a debt **settled** - it drops out of outstanding balances.

---

## Getting started

### Prerequisites
- .NET 10 SDK
- SQL Server **LocalDB** (`(localdb)\MSSQLLocalDB`) - bundled with Visual Studio / SQL Server Express
- (Optional) `dotnet-ef` tool for migrations: `dotnet tool install --global dotnet-ef`

### Database
The connection string lives in [`Pinowo/appsettings.json`](Pinowo/appsettings.json)
(`ConnectionStrings:DefaultConnection`, database `Pinowo`). Apply migrations:

```bash
dotnet ef database update --project Pinowo/Pinowo.csproj
```

### Run

```bash
cd Pinowo
dotnet run
```

By default Kestrel binds to its configured port; for a fixed HTTP port:

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 dotnet run
```

### Seed demo data

```bash
dotnet run -- seed
```

Wipes all domain data and recreates two demo groups. **Re-run this right before a demo** -
settling debts or adding expenses during testing mutates the data.

**Demo logins** - the password is set via the `PINOWO_SEED_PASSWORD` environment
variable before running the seeder (e.g. `set PINOWO_SEED_PASSWORD=YourPick` on
Windows). If it's not set, the seeder generates a random password and prints it to
the console. The same password applies to every demo user below.

| Email | Name | Groups |
|---|---|---|
| `amara@pinowo.demo` | Amara Okafor | Lagos Apartment + Concert Weekend |
| `chidi@pinowo.demo` | Chidi Eze | Lagos Apartment |
| `bisi@pinowo.demo` | Bisi Adeyemi | Lagos Apartment |
| `tunde@pinowo.demo` | Tunde Bello | Concert Weekend |

Log in as **Amara** - she's in both groups and is a creditor in each, ideal for the demo.

---

## REST API (PRD Section 6)

All endpoints return JSON. Authenticated endpoints use the Identity auth cookie
(log in via `/api/users/login` first; with curl, persist cookies with `-c`/`-b`).

| Method | Route | Notes |
|---|---|---|
| POST | `/api/users/register` | name, email, password |
| POST | `/api/users/login` | sets auth cookie |
| POST | `/api/groups` | create group (caller becomes first member) |
| GET | `/api/groups/{id}` | group + members (member-only) |
| POST | `/api/groups/{id}/members` | add a user to the group |
| POST | `/api/groups/{id}/expenses` | add expense (equal split) |
| GET | `/api/groups/{id}/expenses` | list expenses + shares |
| GET | `/api/groups/{id}/balances` | net who-owes-whom + USD reference |
| POST | `/api/expenses/{id}/shares/{shareId}/settle` | mark a share settled |
| GET | `/api/rates/current` | latest BTC & USDT USD rates (cached) |

Example:

```bash
curl -c cookies.txt -X POST http://localhost:5080/api/users/login \
  -H "Content-Type: application/json" \
  -d '{"email":"amara@pinowo.demo","password":"<your-seed-password>"}'

curl -b cookies.txt http://localhost:5080/api/groups/1/balances
```

---

## Real-time updates

The MVC group page subscribes to the `/hubs/balances` SignalR hub and joins a per-group
channel **only after the server verifies group membership**. When any client adds an
expense or settles a share, the server pushes `BalancesChanged` to that group and connected
clients re-fetch `GET /api/groups/{id}/balances` and re-render - no page refresh.

The SignalR client is served **locally** (`wwwroot/lib/signalr/`), and the balances panel
renders from the REST API **first**; live updates are layered on top, so the panel still
works if the real-time channel is unavailable.

---

## Tests

```bash
dotnet test
```

`Pinowo.Tests` covers the balance engine, including a 3-user / 3-expense fixture with
hand-verified expected output, the empty-group edge case, and equal-split exactness
(no satoshi created or lost).

---

## Notable design decisions

- **Identity with int keys** - `User : IdentityUser<int>` keeps the data model's integer
  foreign keys intact while letting Identity manage credentials (one user table).
- **Payer is included in the split** - the payer's own share nets out in the calculation
  (standard Splitwise model).
- **Settle is one-way** for the MVP (no un-settle) - the PRD only asks to mark debts settled.
- **Rate caching + offline fallback** - CoinGecko is called at most once per ~5 min per
  currency; if unreachable, the last snapshot (or a seeded bootstrap value) is used so the
  app stays usable.
