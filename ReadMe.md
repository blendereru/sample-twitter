## SampleTwitter
[![CI](https://github.com/blendereru/sample-twitter/actions/workflows/ci.yml/badge.svg)](https://github.com/blendereru/sample-twitter/actions/workflows/ci.yml)

A sample twitter app(pure CRUD), intended to be a practice project. Backend written in c#/.net core using simple
`MVC` pattern with `EF Core` as `ORM`, and client-side scripts written fully in Vue.js/typescript. 
Not caring about front-end part, 100% `slopping`. 

## Architecture
* `MVC` - easy to start with. Planning to rewrite using `VSA`.
* `EF Core` as `ORM` with `PostgreSQL` as db provider.
* `Serilog` - logging. Uses both `Console` and `Seq` as sinks
* `Cookie-based auth` - reasonable to use with browser as a client.
* `TestContainers` + `WebApplicationFactory` - imitate real world scenario during testing.
* `OpenApi` + `Scalar` - UI is just so beautiful.
* `Architecture Tests` - set some rules in the source code. This ensures boundaries are not crossed, and that all methods/classes
follow the same standard/approach.

## Getting Started

### Prerequisites
* Docker and Docker Compose installed on your machine.

### Environment Configuration
The project uses environment variables defined in `.env`. An example template is provided in `.env.example`.

1. Copy `.env.example` to `.env`:
   ```bash
   cp .env.example .env
   ```
   On Windows PowerShell:
   ```powershell
   Copy-Item .env.example .env
   ```

2. Configure the variables in `.env`:
   * `DB_PASSWORD`: Password for the PostgreSQL database user (`postgres`).
   * `SEQ_FIRSTRUN_ADMINPASSWORD`: Initial administrator password for the Seq logging server.
   * `SEQ_API_KEY`: API key for Seq log ingestion.
   * `BASE_URL`: Base URL for the application (defaults to `http://localhost:3000`).
   * `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM_ADDRESS`, `SMTP_FROM_NAME`: SMTP email server credentials used for sending account confirmation emails.

### Running the Application with Docker Compose
1. Build and start all services:
   ```bash
   docker compose up --build
   ```
   To run in the background (detached mode):
   ```bash
   docker compose up -d --build
   ```

2. Access the running services:
   * Web Application: http://localhost:3000
   * Seq Log Dashboard: http://localhost:8081
   * PostgreSQL Database: localhost:5432

3. Stop the services:
   ```bash
   docker compose down
   ```
   To also remove persistent database and log volumes:
   ```bash
   docker compose down -v
   ```

## What is planned but not implemented yet?
1) Want to implement `SSO` scenarios using `Google` provider. 
2) Use `Aspire` for integration tests. Will probably publish as v2.
3) `e2e tests`

... And a bunch of other stuff.

## License
The project is distributed under the terms of the [MIT license](https://github.com/blendereru/sample-twitter/blob/main/LICENSE).