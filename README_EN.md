# MindSite

Web Platform with an Intelligent Chatbot for Intermediation, Monitoring, and Management of Digital Services.

This project was developed under the guidance and tutorship of professors **Douglas dos Reis** and **Lincoln Bezerra de Souza** as a Capstone Project (TCC) for the Systems Development Technical Course at **SENAI "Mariano Ferraz" School**. The application focuses on solving scope and communication issues when closing software contracts by integrating payment, messaging, and authentication tools.

---

## 📊 Table of Contents

- [MindSite](#mindsite)
  - [📊 Table of Contents](#-table-of-contents)
  - [📌 Overview](#-overview)
  - [⚙️ Features](#️-features)
  - [🌐 APIs and External Integrations](#-apis-and-external-integrations)
  - [🧰 Tech Stack](#-tech-stack)
  - [📁 Project Structure](#-project-structure)
  - [👤 Team](#-team)

---

## 📌 Overview

**MindSite** is a platform that bridges the gap between clients and technology providers by automating contracts, payments, and notifications.

The project objective is to demonstrate:
- A robust enterprise architecture utilizing the **MVC (Model-View-Controller)** pattern, scaling it toward an **N-Tier Architecture**.
- Relational database persistence and migrations using **Entity Framework Core**.
- Bi-directional communication and asynchronous real-time updates via **SignalR**.
- Infrastructure isolation and portability through **Docker and Docker Compose** containers.
- Scalable cloud deployment leveraging **Azure App Service**.

---

## ⚙️ Features

- **Federated Authentication:** Secure native login and Identity Provider integration via Google OAuth 2.0.
- **Project Onboarding:** An intelligent requirements-gathering system based on the client's project scope.
- **Real-Time Chat & Communication:** An integrated asynchronous messaging hub tailored for negotiation between parties.
- **Integrated Financial Management:** Transparent checkout and automated transaction-based escrow/payment custody.
- **Automated Notifications:** Asynchronous high-performance dispatch of transactional emails for project status updates and security alerts.
- **Live Documentation:** Automatic route exposure and endpoint mapping.

---

## 🌐 APIs and External Integrations

- **Google Authentication API:** Secure federated identity mechanism leveraging OAuth 2.0.
- **Stripe API Gateway:** Payment platform handling checkouts, webhooks (`checkout.session.completed`), and transaction management.
- **Resend Email API:** High-performance asynchronous gateway for transactional email delivery and custom domain notifications.

---

## 🧰 Tech Stack

- **Backend:** .NET 10 (ASP.NET Core MVC)
- **Frontend:** HTML5, CSS3, Tailwind CSS, JavaScript (ES6+), SignalR Client
- **Database:** MySQL 8.0 & Pomelo Entity Framework Core
- **Infrastructure & Cloud:** Docker, Docker Compose, Azure CLI, and Azure App Service
- **Documentation:** Swagger UI / OpenAPI Spec v3

---

## 📁 Project Structure

```text
MindSite/
│
├── Controllers/            # MVC Architecture Controllers
├── Data/                   # EF Core Context (AppDbContext) and Migrations
├── Filters/                # Custom Request and TempData Filters
├── Hubs/                   # SignalR Connections Configuration (Real-Time Chat)
├── Interfaces/             # Service Contracts (IArquivoStorage, IEmailService)
├── Models/                 # Data Models and Business Entities
├── Services/               # Logic Implementations (Stripe, Resend, Notifications)
├── Views/                  # Frontend User Interfaces (Razor Pages)
├── wwwroot/                # Static files (JavaScript, CSS, local uploads)
│
├── Dockerfile              # Multi-stage production build script for .NET 10
├── docker-compose.yml      # Local development orchestration
├── Program.cs              # Application bootstrapper, middlewares, and Dependency Injection
└── README.md               # Main documentation file
```

## 👤 Team

[Alberto Ziurkelis de Araujo](https://github.com/AlbertZiurk)

[Guilherme Beltrame Nery](https://github.com/GuilhermebNery)

[João Pedro Serignolli Borin](https://github.com/jpserignolli)

[Josué Gumer Mamani Ticona](https://github.com/yehoshuajosue)

[Lucas Donato de Souza](https://github.com/Donatinnho)

📄 License

This project is licensed under the MIT License.

![MIT License](https://img.shields.io/badge/License-MIT-green.svg)
