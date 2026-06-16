# MindSite

Plataforma Web com Chatbot Inteligente para Intermediação, Monitoramento e Gestão de Serviços Digitais.

Este projeto foi desenvolvido sob a orientação e tutoria dos docentes **Douglas dos Reis** e **Lincoln Bezerra de Souza** como Trabalho de Conclusão de Curso (TCC) para o Curso Técnico em Desenvolvimento de Sistemas na **Escola SENAI "Mariano Ferraz"**. A aplicação foca na resolução de problemas de escopo e comunicação no fechamento de contratos de software, integrando ferramentas de pagamento, mensageria e autenticação.

---

## 📊 Sumário

- [MindSite](#mindsite)
  - [📊 Sumário](#-sumário)
  - [📌 Visão Geral](#-visão-geral)
  - [⚙️ Funcionalidades](#️-funcionalidades)
  - [🌐 APIs e Integrações Externas](#-apis-e-integrações-externas)
  - [🧰 Tecnologias](#-tecnologias)
  - [📁 Estrutura do Projeto](#-estrutura-do-projeto)
  - [👤 Equipe](#-equipe)
  - [📄 Licença](#-licença)

---

## 📌 Visão Geral

O **MindSite** é uma plataforma que intermedia a ponte entre clientes e fornecedores de tecnologia, automatizando contratos, pagamentos e notificações.

O objetivo do projeto é demonstrar:
- Arquitetura corporativa robusta utilizando o padrão **MVC (Model-View-Controller)** e alterando-o para **Arquitetura de N camadas**.
- Persistência e migração de banco de dados relacional com **Entity Framework Core**.
- Comunicação bidirecional e atualizações assíncronas em tempo real com **SignalR**.
- Isolamento e portabilidade de infraestrutura através de contêineres **Docker e Docker Compose**.
- Deploy escalável em nuvem utilizando **Azure App Service**.

---

## ⚙️ Funcionalidades

- **Autenticação Federada:** Login seguro nativo e integração com Provedor de Identidade via Google OAuth 2.0.
- **Onboarding de Projetos:** Sistema inteligente de captação de requisitos baseado no escopo do cliente.
- **Chat e Comunicação em Tempo Real:** Hub de mensagens assíncronas integrado para negociação entre as partes.
- **Gestão Financeira Integrada:** Checkout transparente e custódia de pagamentos automatizada por transações.
- **Notificações Automatizadas:** Disparos automáticos de e-mails transacionais para status de projetos e alertas de segurança.
- **Documentação viva:** Exposição automática de rotas e mapeamento de endpoints.

---

## 🌐 APIs e Integrações Externas

- **Google Authentication API:** Mecanismo de login federado seguro utilizando OAuth 2.0.
- **Stripe API Gateway:** Plataforma de pagamentos para checkout, webhook de confirmação (`checkout.session.completed`) e gestão de transações.
- **Resend Email API:** Gateway assíncrono de alto desempenho para disparo de e-mails transacionais e notificações de domínio.

---

## 🧰 Tecnologias

- **Backend:** .NET 10 (ASP.NET Core MVC)
- **Frontend:** HTML5, CSS3, Tailwind CSS, JavaScript (ES6+), SignalR Client
- **Banco de Dados:** MySQL 8.0 & Pomelo Entity Framework Core
- **Infraestrutura & Nuvem:** Docker, Docker Compose, Azure CLI e Azure App Service
- **Documentação:** Swagger UI / OpenAPI Spec v3

---

## 📁 Estrutura do Projeto

```text
MindSite/
│
├── Controllers/            # Controladores da arquitetura MVC
├── Data/                   # Contexto do EF Core (AppDbContext) e Migrations
├── Filters/                # Filtros customizados de requisições e TempData
├── Hubs/                   # Configuração das conexões SignalR (Chat em Tempo Real)
├── Interfaces/             # Contratos de serviços (IArquivoStorage, IEmailService)
├── Models/                 # Modelos de dados e entidades de negócio
├── Services/               # Implementações lógicas (Stripe, Resend, Notificações)
├── Views/                  # Páginas e interfaces do frontend (Razor Pages)
├── wwwroot/                # Arquivos estáticos (JavaScript, CSS, uploads locais)
│
├── Dockerfile              # Script de build de produção multi-stage do .NET 10
├── docker-compose.yml      # Orquestração
├── Program.cs              # Inicializador da aplicação, middlewares e injeção de dependências
└── README.md               # Documentação principal do projeto

## 👤 Equipe

[Alberto Ziurkelis de Araujo](https://github.com/AlbertZiurk)
[Guilherme Beltrame Nery] (https://github.com/GuilhermebNery)
[João Pedro Serignolli Borin] (https://github.com/jpserignolli)
[Josué Gumer Mamani Ticona] (https://github.com/yehoshuajosue)
[Lucas Donato de Souza] (https://github.com/Donatinnho)

## 📄 Licença

Este projeto está licenciado sob a MIT

![MIT License](https://img.shields.io/badge/License-MIT-green.svg)