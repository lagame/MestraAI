# MestrAI

> **RPG + IA, do zero ao combate.**  
> Plataforma ASP.NET Core para **mestrar sessões de RPG** com **IA orquestrando NPCs**, chat em tempo‑real, streaming/SSE, SignalR, controle de mídia e telemetria.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Web-1f6feb)](https://learn.microsoft.com/aspnet/core)
[![EF Core](https://img.shields.io/badge/EF%20Core-Sqlite-2c3e50)](https://learn.microsoft.com/ef/core/)
[![NUnit](https://img.shields.io/badge/Tests-NUnit%204-green)](https://nunit.org/)
[![Playwright](https://img.shields.io/badge/E2E-Playwright%20for%20.NET-45ba4b)](https://playwright.dev/dotnet/)
[![Swagger](https://img.shields.io/badge/API-Swagger-85ea2d)](https://swagger.io/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](#licenca)

---

## Índice
- [Arquitetura](#arquitetura)
- [Principais recursos](#principais-recursos)
- [Stack técnica](#stack-técnica)
- [Estrutura de pastas](#estrutura-de-pastas)
- [Guia rápido (Windows / Linux)](#guia-rápido-windows--linux)
- [Configuração](#configuração)
  - [Conexão com banco (SQLite)](#conexão-com-banco-sqlite)
  - [IA: Provider Local e Gemini](#ia-provider-local-e-gemini)
  - [Portas / Kestrel](#portas--kestrel)
- [Execução](#execução)
- [APIs / Endpoints](#apis--endpoints)
- [Hubs (SignalR)](#hubs-signalr)
- [Testes (Unit / E2E)](#testes-unit--e2e)
- [Boas práticas de versionamento](#boas-práticas-de-versionamento)
- [Roadmap](#roadmap)
- [Contribuindo](#contribuindo)
- [Licença](#licenca)

---

## Arquitetura

**Monolito ASP.NET Core** com camadas explícitas:

- **Controllers** para endpoints REST (`/api/*`) e streaming SSE.
- **SignalR Hubs** para chat/battlemap em tempo‑real.
- **Services** para regras de domínio (chat, rolagens, mídia, IA, telemetria).
- **Data** com **Entity Framework Core** e **Identity**.
- **Models/Dtos/ViewModels** para tipos fortemente tipados.
- **wwwroot** para assets estáticos controlados via **LibMan**.

O **orquestrador de IA** centraliza a conversa de NPCs (gera fala, decide memórias, grava telemetria), delegando o provedor ativo (Local/Gemini) via **factory** configurável.

## Principais recursos

- **Chat e combate em tempo real** com **SignalR** (`ChatHub`, `BattlemapHub`).
- **SSE** para streaming de mensagens: `/sessions/{sessionId}/chat/stream`.
- **Orquestração de IA**: geração de respostas de NPC e **decisão de memória** automática.
- **Múltiplos provedores de IA**: `Local` (HTTP) e `Gemini` (Vertex AI) selecionáveis por configuração.
- **Métricas & Telemetria de NPCs e Sistema** (endpoints administrativos).
- **Uploads de mídia** (imagens/áudios) com **ImageSharp**.
- **Autenticação/Autorização** com **Identity** e **roles** (`Admin`, `Narrator`, `User`).
- **Swagger** para documentação interativa.
- **Seed/Migrate** automáticos no startup (migrations + `DevSeeder` + `NpcSystemSeeder`).

## Stack técnica

- **.NET 8**, **ASP.NET Core**, **Razor Pages + APIs**
- **EF Core** (`Microsoft.EntityFrameworkCore.Sqlite`)
- **Identity** (UI + EntityFrameworkCore)
- **SignalR** (Chat/Battlemap)
- **Swagger** (`Swashbuckle.AspNetCore`)
- **HtmlSanitizer** (higienização de entrada)
- **SixLabors.ImageSharp** (processamento de imagens)
- **NUnit + Moq + Playwright** (unit + E2E)
- **LibMan** (Bootstrap/JS libs em `wwwroot/lib`)

> Pacotes relevantes no projeto: AspNetCoreRateLimit, Google.Apis.Auth, NJsonSchema, etc.

## Estrutura de pastas

```
MestraAI/
├─ MestrAI.sln
├─ MestrAI/                      # Web app
│  ├─ Controllers/               # REST + SSE
│  ├─ Hubs/                      # SignalR (ChatHub/BattlemapHub)
│  ├─ Services/                  # Regras, Orquestrador de IA, Providers
│  ├─ Data/                      # ApplicationDbContext, Migrations, Seeders
│  ├─ Models/ Dtos/ ViewModels/  # Tipos de domínio
│  ├─ HealthChecks/              # Healthchecks (ex.: SignalRHealthCheck)
│  ├─ Prompts/ Ai/ Config/       # Artefatos de IA
│  ├─ wwwroot/                   # Estáticos (lib via LibMan)
│  ├─ appsettings*.json
│  └─ MestrAI.csproj
└─ MestrAI.Tests/                # NUnit + Playwright
```

> Árvore gerada automaticamente a partir do repositório em 2025-09-29.

## Guia rápido (Windows / Linux)

Pré-requisitos:
- **.NET SDK 8.0+**
- **LibMan CLI**: `dotnet tool install -g Microsoft.Web.LibraryManager.CLI`
- (Linux) **PowerShell 7** para scripts do Playwright: `sudo snap install powershell --classic`

Instalação local:

```bash
# Restaurar dependências
libman restore
dotnet restore

# Criar/atualizar banco (SQLite)
dotnet tool install -g dotnet-ef   # 1ª vez
dotnet ef database update --project MestrAI/MestrAI.csproj

# Build & Test
dotnet build
dotnet test
```

## Configuração

### Conexão com banco (SQLite)

`appsettings.json` já aponta para um arquivo local (`DataSource=app.db;Cache=Shared`).  
Para trocar por outro SQLite ou SQL Server, altere:

```jsonc
// MestrAI/appsettings.json
{{
  "ConnectionStrings": {{
    "DefaultConnection": "DataSource=app.db;Cache=Shared"
  }}
}}
```

### IA: Provider Local e Gemini

Seleção do provedor e parâmetros (exemplo **existente** no projeto):

```jsonc
// MestrAI/appsettings.json
"Ai": {{
  "Provider": "Local",
  "Local": {{
    "ContextServiceUrl": "http://localhost:5001",
    "MaxTokens": 4096,
    "TimeoutSeconds": 30,
    "MaxRetries": 2
  }}
}}
```

- **Local**: espera um serviço HTTP próprio (Context Service) para geração/embeddings/merge.
- **Gemini (Vertex AI)**: habilite trocando `"Provider": "Gemini"` e forneça credenciais via
  **Google ADC** (`GOOGLE_APPLICATION_CREDENTIALS` apontando para o JSON de service account)
  e parâmetros de modelo/endpoint conforme o seu ambiente.  
  Veja `Services/GeminiProvider.cs` para o fluxo de chamada.

### Portas / Kestrel

- **`launchSettings.json` (Dev)**: `http://localhost:5117` (VS/`dotnet run` em Dev).  
- **`appsettings.json` (Kestrel)**: `"http://localhost:5050"` configurado em `Kestrel:Endpoints:Http:Url`.

A porta efetiva aparece no console ao iniciar a aplicação.

## Execução

Via CLI:

```bash
dotnet run --project MestrAI/MestrAI.csproj
# Abra: http://localhost:5117 (Dev) ou a porta exibida no console
# Swagger: /swagger
```

Publicação (Release):

```bash
dotnet publish MestrAI/MestrAI.csproj -c Release -o out
# deploy o conteúdo de /out para seu host (Nginx/Apache/IIS/Docker)
```

Variáveis úteis em produção:
- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__DefaultConnection="..."`
- `Ai__Provider=Local|Gemini`
- `Ai__Local__ContextServiceUrl="..."`

## APIs / Endpoints

BasePaths e exemplos (autorização via Identity; alguns exigem `Admin`/`Narrator`):

- **ChatController** — base `/api/Chat`
  - `POST /api/Chat/sessions/{{sessionId}}/messages`
  - `GET  /api/Chat/sessions/{{sessionId}}/messages`
  - `GET  /api/Chat/sessions/{{sessionId}}/messages/recent`

- **ChatStreamController** — base `/sessions/{{sessionId}}/chat`
  - `GET  /sessions/{{sessionId}}/chat/stream` _(SSE)_

- **AiController** — base `/api/Ai` (`Roles: Narrator,Admin`)
  - `POST /api/Ai/character/act` — chama o **AiOrchestrator** para gerar fala do personagem/NPC.

- **AiNpcController** — base `/api/AiNpc`
  - `POST /api/AiNpc/set-status`
  - `GET  /api/AiNpc/session/{{sessionId}}/npcs`
  - `GET  /api/AiNpc/session/{{sessionId}}/audit`
  - `GET  /api/AiNpc/character/{{characterId}}/audit`
  - `GET  /api/AiNpc/session/{{sessionId}}/metrics`

- **NpcTelemetryController** — base `/api/NpcTelemetry` (`Roles: Admin,Narrator`)
  - `GET /api/NpcTelemetry/session/{{sessionId}}`
  - `GET /api/NpcTelemetry/system`

- **MediaController** — base `/api/Media`
  - `POST   /api/Media/upload` (upload)
  - `GET    /api/Media/session/{{sessionId}}`
  - `GET    /api/Media/file/{{mediaId}}`
  - `POST   /api/Media/audio/play`
  - `POST   /api/Media/audio/stop`
  - `DELETE /api/Media/{{mediaId}}`

> Documentação interativa em **`/swagger`** (ativada em Development/Test).

## Hubs (SignalR)

- `"/chathub"` — mensagens, presença, notificações.
- `"/battlemaphub"` — sincronização do mapa, tokens/camadas.

## Testes (Unit / E2E)

- **Unit** com **NUnit + Moq + EF InMemory**.
- **E2E** com **Playwright for .NET** (NUnit).

Primeira execução dos testes E2E (instala browsers do Playwright):

```bash
dotnet build
pwsh ./MestrAI.Tests/bin/Debug/net8.0/playwright.ps1 install --with-deps
dotnet test
```

## Boas práticas de versionamento

- **Commits**: Conventional Commits (`feat:`, `fix:`, `chore:`, `test:`, `docs:`…).
- **Branches**: `main` (estável) / `feat/*` / `fix/*` / `hotfix/*`.
- **SemVer**: `vMAJOR.MINOR.PATCH`.
- **Migrations**: sempre versionadas. Nomeie com contexto (`AddNpcAudit`, `RefactorChatMessage`).

## Roadmap

- [ ] Painel administrativo para telemetria/saúde.
- [ ] Alternância de provedores de IA via UI.
- [ ] Hardening de segurança (CSP, rate-limit configurado).
- [ ] Export de transcrições e memórias.
- [ ] Testes E2E adicionais (fluxo completo de sessão).

## Contribuindo

1. Abra uma issue descrevendo a melhoria/bug.
2. Crie branch `feat/*` ou `fix/*` a partir de `main`.
3. Rode **lint/build/test** localmente.
4. Abra um PR com descrição e _checklist_ de testes.

## Licença

Distribuído sob a licença **MIT**. Veja `LICENSE` (adicione o arquivo conforme sua preferência).

---

> **Nota:** Este README foi gerado automaticamente a partir do conteúdo do repositório e ajustado para o estado atual do projeto.
