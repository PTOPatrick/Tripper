# Tripper - Group Travel Planner

An MVP application for planning group trips, managing expenses, and voting on destinations.

## Tech Stack
- **Backend**: .NET 10, ASP.NET Core Minimal APIs, EF Core, PostgreSQL
- **Frontend**: Angular 18+, TailwindCSS
- **Infrastructure**: Docker Compose (PostgreSQL)

## Prerequisites
- .NET 10 SDK
- Node.js 20+
- Docker Desktop

## Setup & Running

1. **Start Database**
   ```bash
   docker compose up -d
   ```

2. **Run Backend**
   ```bash
   cd Tripper.API
   dotnet run
   ```
   API will be available at `http://localhost:5208`.
   Swagger UI: `http://localhost:5208/swagger` (if enabled) or access `http://localhost:5208/openapi/v1.json`.

3. **Run Frontend**
   ```bash
   cd frontend
   npm install
   ng serve
   ```
   Access the app at `http://localhost:4200`.

## Features
- **Auth**: Sign up and Login.
- **Groups**: Create groups, add members.
- **Expenses**: Add shared expenses, view balances.
- **Voting**: Vote on destination cities/countries.

## API Documentation
The API documentation is available via OpenAPI/Swagger when running in Development mode.

## Testing
Core business logic is enforced via API endpoints.
- Integration tests project `Tripper.Tests` is available but may require preview packages for .NET 10.
