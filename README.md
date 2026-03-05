# Airline Customer Support Ticketing System

An enterprise-grade backend system for post-booking airline customer support. Built with Clean Architecture and Domain-Driven Design principles on ASP.NET Core 8.

## Purpose

A comprehensive support ticketing platform that enables passengers to create support requests for flight cancellations, refunds, baggage issues, delays, and more. Support agents manage these tickets with AI-powered automatic classification, priority detection, and reply drafting powered by Google Gemini.

## Features

### Architecture
- **Clean Architecture**: Strict separation across Domain, Application, Infrastructure, and API layers
- **Domain-Driven Design (DDD)**: Rich domain entities, domain services, and value objects
- **CQRS Pattern**: Command and Query separation for use-case driven business logic

### Authentication & Authorization
- **JWT Bearer Authentication**: Role-based access control (Passenger, SupportAgent, Admin)
- **PNR Verification**: Passengers authenticate via PNR + last name against reservation data
- **Secure Password Storage**: PBKDF2-SHA256 with 600,000 iterations and timing-safe comparison

### Ticket Management
- **Strict State Machine**: `New → Triaged → Assigned → InProgress → WaitingOnPassenger → Resolved → Closed` lifecycle
- **Invalid transition protection**: Transitions violating state machine rules are automatically rejected
- **Reopen mechanism**: Resolved tickets can be reopened when needed (`Resolved → InProgress`)
- **Internal notes**: Agents/Admins can add notes invisible to passengers

### SLA Monitoring
- **First Response SLA**: 2 hours
- **Resolution SLA**: 24 hours
- **Auto-Escalation**: Priority automatically elevated on SLA breach
- **Auto-Close**: Tickets automatically closed 72 hours after resolution
- **Background Service**: `SlaMonitorService` periodically monitors SLA compliance

### AI Integration (Google Gemini)
- **Automatic Classification**: Passenger messages analyzed for category (11 categories) and priority (P0–P3)
- **Smart Reply Drafting**: Generates agent reply drafts with policy citations, risk flags, and suggested actions
- **Embedding-Based Semantic Search (RAG)**: Searches knowledge base policies by meaning, not just keywords
- **Fallback Mechanism**: Automatically switches to keyword-based mock system if Gemini API is unavailable

### Knowledge Base
- **Policy Documents**: Admins create and publish policy documents
- **Automatic Chunking**: Published policies are split into searchable chunks
- **RAG (Retrieval-Augmented Generation)**: Relevant policy sections are automatically cited in agent replies

### Audit Trail
- **Append-only event log**: Every action (creation, assignment, transition, message) is recorded
- **Actor tracking**: Full traceability of who did what and when

### Input Validation
- **FluentValidation**: Input validation on all commands
- **Global ValidationFilter**: Invalid requests are rejected before reaching controllers

## Project Structure

```
AirlineTicketing/
├── src/
│   ├── Support.Domain/            # Entities, Enums, Domain Services
│   │   ├── Entities/              # Ticket, User, PolicyDocument, PolicyChunk, ...
│   │   ├── Enums/                 # TicketState, Priority, TicketCategory, Role, ...
│   │   ├── Services/              # TicketStateMachine
│   │   └── Common/                # BaseEntity
│   │
│   ├── Support.Application/       # Business Logic Layer
│   │   ├── Features/
│   │   │   ├── Auth/              # Login, VerifyPnr (Command + Handler + Validator)
│   │   │   ├── Tickets/           # CreateTicket, AddMessage, AssignTicket, ...
│   │   │   └── Policies/          # CreatePolicy, PublishPolicy, GetPolicyById
│   │   ├── Interfaces/            # IAiCopilotClient, IPolicySearchService, ...
│   │   ├── Models/                # ReservationInfo, PolicyCitation, DTOs
│   │   └── Common/                # Result<T> pattern
│   │
│   ├── Support.Infrastructure/    # External Dependencies
│   │   ├── Persistence/           # ApplicationDbContext, DbSeeder, Migrations
│   │   ├── Services/              # Gemini AI, Mock AI, JWT, PasswordHasher, ...
│   │   └── BackgroundServices/    # SlaMonitorService
│   │
│   └── Support.Api/               # HTTP API Layer
│       ├── Controllers/           # Auth, Tickets, Agent, Policies
│       └── Filters/               # ValidationFilter
│
├── tests/
│   ├── Support.Application.Tests/       # Unit tests (State Machine)
│   └── Support.Api.IntegrationTests/    # Integration tests (12 tests)
│
└── AirlineSupport.slnx
```

## Setup

### Prerequisites
- .NET 8 SDK
- SQL Server (LocalDB or SQLEXPRESS)

### 1. Configure Secrets

```bash
cd src/Support.Api
dotnet user-secrets init

# JWT secret (minimum 32 characters)
dotnet user-secrets set "Jwt:Secret" "YourSuperSecretKeyMinimum32CharactersLongForHS256Algorithm"

# Gemini AI API key (optional - runs in mock mode without it)
# Get a free key at: https://aistudio.google.com
dotnet user-secrets set "Gemini:ApiKey" "your-gemini-api-key"
```

### 2. Create Database

```bash
# Check connection string in src/Support.Api/appsettings.json
# Apply migrations
dotnet ef database update --project src/Support.Infrastructure --startup-project src/Support.Api
```

### 3. Run the API

```bash
dotnet run --project src/Support.Api
```

The API starts at `http://localhost:5098`.

Swagger UI: [http://localhost:5098/swagger](http://localhost:5098/swagger)

> **Note:** If a Gemini API key is configured, the AI runs on real Gemini. If not, the application automatically falls back to mock mode. The app is fully functional in both modes.

## API Endpoints

### Authentication (Public)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/login` | Agent/Admin login (email + password) |
| POST | `/api/auth/passenger/verify-pnr` | Passenger PNR verification |

### Passenger Operations
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/tickets` | Create support ticket |
| GET | `/api/tickets/mine` | List my tickets |
| GET | `/api/tickets/{id}` | Get ticket details |
| POST | `/api/tickets/{id}/messages` | Add message to ticket |

### Agent/Admin Operations
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/agent/queue` | Ticket queue (filter by state, priority, category, SLA risk) |
| GET | `/api/agent/my-queue` | Tickets assigned to me |
| POST | `/api/tickets/internal` | Create internal ticket (PNR optional) |
| POST | `/api/tickets/{id}/assign` | Assign ticket to agent |
| POST | `/api/tickets/{id}/transition` | Transition ticket state (enforces state machine) |
| POST | `/api/tickets/{id}/internal-notes` | Add internal note (hidden from passengers) |

### Policy Management (Admin)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/policies` | Create policy document (draft) |
| POST | `/api/policies/{id}/publish` | Publish policy and index for RAG |
| GET | `/api/policies/{id}` | Get policy details |

## Demo Data

The database is automatically seeded on first creation:

### Users
| Role | Email | Password |
|------|-------|----------|
| Admin | `admin@airline.com` | `Admin123!` |
| Agent | `agent@airline.com` | `Agent123!` |

### Test PNRs (Passenger Verification)
| PNR | Last Name | Passenger | Flight | Status |
|-----|-----------|-----------|--------|--------|
| `ABC123` | `Doe` | John Doe | AA100 JFK→LAX (7 days out) | On-time, Refundable |
| `XYZ789` | `Smith` | Jane Smith | BA200 LHR→CDG (2 days out) | Delayed, Non-refundable |

### Sample Policy
The system automatically creates a "Flight Cancellation and Refund Policy" document with 9 searchable chunks on startup.

## End-to-End Test Scenario

Follow these steps to test all features via Swagger:

1. **Passenger Login**: `POST /api/auth/passenger/verify-pnr` → PNR: `ABC123`, lastName: `Doe`
2. **Set Token**: Paste `Bearer <token>` in Swagger's Authorize dialog
3. **Create Ticket**: `POST /api/tickets` → subject, description, pnr, passengerLastName
4. **Agent Login**: `POST /api/auth/login` → `agent@airline.com` / `Agent123!`
5. **Set Agent Token**: Paste new token in Authorize
6. **Assign Ticket**: `POST /api/tickets/{id}/assign` → your agentId
7. **Send Reply**: `POST /api/tickets/{id}/messages` → reply to passenger
8. **Add Internal Note**: `POST /api/tickets/{id}/internal-notes` → agent-only note
9. **Advance State**: `POST /api/tickets/{id}/transition` → newState: 3 (InProgress)
10. **Resolve & Close**: newState: 5 (Resolved), then newState: 6 (Closed)

## Ticket State Machine

```
    ┌─────────────────────────────────────────┐
    │                                         │
    │  New ──→ Triaged ──→ Assigned ──→ InProgress
    │   │                      │          │    ↑
    │   │                      │          │    │
    │   ↓                      ↓          ↓    │
    │  Cancelled          Cancelled   WaitingOnPassenger
    │                                     │
    │                                     ↓
    │                                  Resolved ──→ Closed
    │                                     │
    │                                     └──→ InProgress (reopen)
    └─────────────────────────────────────────┘
```

**Terminal states**: `Closed` and `Cancelled` — no transitions allowed from these states.

## Testing

```bash
# Run all tests (12 tests)
dotnet test

# Unit tests only
dotnet test tests/Support.Application.Tests

# Integration tests only
dotnet test tests/Support.Api.IntegrationTests
```

### Test Coverage
| Test Class | Count | Coverage |
|------------|-------|----------|
| `TicketStateMachineTests` | 5 | State machine transition rules |
| `AgentWorkflowTests` | 1 | End-to-end agent workflow |
| `SecurityAuditTests` | 9 | Role-based access, internal note security, state machine, JWT claims |
| `SlaIdempotencyTests` | 1 | SLA escalation idempotency |
| **Total** | **12** | |

## Technologies

| Technology | Usage |
|------------|-------|
| ASP.NET Core 8 | Web API framework |
| Entity Framework Core 8 | ORM and database management |
| SQL Server (SQLEXPRESS) | Relational database |
| JWT Bearer | Authentication and authorization |
| Google Gemini AI | Ticket classification and reply drafting (gemini-2.0-flash) |
| Google Gemini Embeddings | Semantic policy search (text-embedding-004) |
| FluentValidation 12 | Input validation |
| xUnit | Testing framework |
| Swagger / OpenAPI | API documentation |

## Architectural Decisions

- **Interface Segregation**: AI and search services are abstracted behind interfaces. Mock implementations automatically activate when the Gemini API is unavailable.
- **Fallback Pattern**: If a Gemini API call fails (quota, network error), keyword-based fallback logic runs. The application never crashes.
- **Domain Encapsulation**: All entities use private setters; business rules are enforced within entity methods.
- **Audit Immutability**: Audit events are append-only and cannot be updated or deleted.
- **Test Isolation**: Integration tests use an InMemory database and mock AI services with no external API dependencies.

## License

MIT
