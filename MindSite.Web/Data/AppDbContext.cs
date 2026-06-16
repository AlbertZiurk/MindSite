using Microsoft.EntityFrameworkCore;
using MindSite.Entities;
using MindSite.Enums;

namespace MindSite.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Usuario>            Usuarios            { get; set; }
        public DbSet<Servico>            Servicos            { get; set; }
        public DbSet<Proposta>           Propostas           { get; set; }
        public DbSet<SolicitacaoMudanca> SolicitacoesMudanca { get; set; }
        public DbSet<Mensagem>           Mensagens           { get; set; }
        public DbSet<MensagemGrupo>      MensagensGrupo      { get; set; }
        public DbSet<Notificacao>        Notificacoes        { get; set; }
        public DbSet<LogAcao>            Logs                { get; set; }
        public DbSet<Arquivo>            Arquivos            { get; set; }
        public DbSet<Avaliacao>          Avaliacoes          { get; set; }
        public DbSet<AvaliacaoSistema>   AvaliacoesSistema   { get; set; }
        public DbSet<ItemPortfolio>      Portfolios          { get; set; }

        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);

            // Índice único no e-mail
            mb.Entity<Usuario>()
              .HasIndex(u => u.Email)
              .IsUnique();

            // EmpresaInfo 1-1 com Usuario
            mb.Entity<EmpresaInfo>()
              .HasOne(e => e.Usuario)
              .WithOne(u => u.Empresa)
              .HasForeignKey<EmpresaInfo>(e => e.UsuarioId)
              .OnDelete(DeleteBehavior.Cascade);

            // Servico → Cliente/Fornecedor
            // Restrict evita múltiplos caminhos de cascade no MySQL
            mb.Entity<Servico>()
              .HasOne(s => s.Cliente)
              .WithMany(u => u.ServicosCliente)
              .HasForeignKey(s => s.ClienteId)
              .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<Servico>()
              .HasOne(s => s.Fornecedor)
              .WithMany(u => u.ServicosFornecedor)
              .HasForeignKey(s => s.FornecedorId)
              .OnDelete(DeleteBehavior.Restrict);

            // Mensagens
            mb.Entity<Mensagem>()
              .HasOne(m => m.Remetente)
              .WithMany(u => u.MensagensEnviadas)
              .HasForeignKey(m => m.RemetenteId)
              .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<Mensagem>()
              .HasOne(m => m.Destinatario)
              .WithMany(u => u.MensagensRecebidas)
              .HasForeignKey(m => m.DestinatarioId)
              .OnDelete(DeleteBehavior.Restrict);

            // MensagemGrupo
            mb.Entity<MensagemGrupo>()
              .HasOne(m => m.Remetente)
              .WithMany()
              .HasForeignKey(m => m.RemetenteId)
              .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<MensagemGrupo>()
              .HasOne(m => m.ThreadUser)
              .WithMany()
              .HasForeignKey(m => m.ThreadUserId)
              .OnDelete(DeleteBehavior.Restrict);

            // SolicitacaoMudanca → Solicitante
            mb.Entity<SolicitacaoMudanca>()
              .HasOne(s => s.Solicitante)
              .WithMany()
              .HasForeignKey(s => s.SolicitanteId)
              .OnDelete(DeleteBehavior.Restrict);

            // Proposta → Servico/Fornecedor
            mb.Entity<Proposta>()
              .HasOne(p => p.Servico)
              .WithMany(s => s.Propostas)
              .HasForeignKey(p => p.ServicoId)
              .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<Proposta>()
              .HasOne(p => p.Fornecedor)
              .WithMany(u => u.PropostasFeitas)
              .HasForeignKey(p => p.FornecedorId)
              .OnDelete(DeleteBehavior.Restrict);

            // Decimal: MySQL precisa de precisão explícita
            mb.Entity<Servico>()
              .Property(s => s.Valor)
              .HasPrecision(10, 2);

            mb.Entity<Servico>()
              .Property(s => s.TaxaPlataforma)
              .HasPrecision(10, 2);

            mb.Entity<Proposta>()
              .Property(p => p.Valor)
              .HasPrecision(10, 2);

            mb.Entity<SolicitacaoMudanca>()
              .Property(s => s.ValorAdicional)
              .HasPrecision(10, 2);

            // Arquivo → Servico/Usuario
            mb.Entity<Arquivo>()
              .HasOne(a => a.Servico)
              .WithMany(s => s.Arquivos)
              .HasForeignKey(a => a.ServicoId)
              .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<Arquivo>()
              .HasOne(a => a.Usuario)
              .WithMany(u => u.Arquivos)
              .HasForeignKey(a => a.UsuarioId)
              .OnDelete(DeleteBehavior.Restrict);

            // Avaliacao → Servico/Cliente/Fornecedor
            mb.Entity<Avaliacao>()
              .HasOne(a => a.Servico)
              .WithOne(s => s.Avaliacao)
              .HasForeignKey<Avaliacao>(a => a.ServicoId)
              .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<Avaliacao>()
              .HasOne(a => a.Cliente)
              .WithMany(u => u.AvaliacoesCliente)
              .HasForeignKey(a => a.ClienteId)
              .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<Avaliacao>()
              .HasOne(a => a.Fornecedor)
              .WithMany(u => u.AvaliacoesFornecedor)
              .HasForeignKey(a => a.FornecedorId)
              .OnDelete(DeleteBehavior.Restrict);

            // AvaliacaoSistema → Servico/Usuario
            mb.Entity<AvaliacaoSistema>()
              .HasOne(a => a.Servico)
              .WithMany(s => s.AvaliacoesSistema)
              .HasForeignKey(a => a.ServicoId)
              .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<AvaliacaoSistema>()
              .HasOne(a => a.Usuario)
              .WithMany(u => u.AvaliacoesSistema)
              .HasForeignKey(a => a.UsuarioId)
              .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<AvaliacaoSistema>()
              .HasIndex(a => new { a.ServicoId, a.UsuarioId })
              .IsUnique();

            // ItemPortfolio → Fornecedor
            mb.Entity<ItemPortfolio>()
              .HasOne(p => p.Fornecedor)
              .WithMany(u => u.Portfolio)
              .HasForeignKey(p => p.FornecedorId)
              .OnDelete(DeleteBehavior.Cascade);

            //Senha hash pra usuários padrões

            // var hashPassword = BCrypt.Net.BCrypt.HashPassword("123456");
            // var hashPassword = BCrypt.Net.BCrypt.HashPassword("123456");


            // SEED — Admins padrões
            mb.Entity<Usuario>().HasData(
                new Usuario
                {
                    Id           = 1,
                    NomeCompleto = "Josué Gumer",
                    Email        = "josuegumer364@gmail.com",
                    Telefone     = "(11) 96743-1518",
                    SenhaHash    = "$2a$12$UeHf1Yo1INS.J2enCd.kruqxNqtK4AAx59cFHVE5uHft2oOJtZYvS",
                    Role         = UserRole.Admin,
                    Status       = UserStatus.Ativo,
                    CriadoEm     = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Usuario
                {
                    Id           = 2,
                    NomeCompleto = "Alberto Ziurkelis",
                    Email        = "albertoZiurk@gmail.com",
                    Telefone     = "(11) 99999-9999",
                    SenhaHash    = "$2a$12$UeHf1Yo1INS.J2enCd.kruqxNqtK4AAx59cFHVE5uHft2oOJtZYvS",
                    Role         = UserRole.Admin,
                    Status       = UserStatus.Ativo,
                    CriadoEm     = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Usuario
                {
                    Id           = 3,
                    NomeCompleto = "Guilherme Beltrame",
                    Email        = "neryDio@gmail.com",
                    Telefone     = "(11) 88888-8888",
                    SenhaHash    = "$2a$12$UeHf1Yo1INS.J2enCd.kruqxNqtK4AAx59cFHVE5uHft2oOJtZYvS",
                    Role         = UserRole.Admin,
                    Status       = UserStatus.Ativo,
                    CriadoEm     = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Usuario
                {
                    Id           = 4,
                    NomeCompleto = "Alberto Cliente",
                    Email        = "alberto@gmail.com",
                    Telefone     = "(11) 88888-8888",
                    SenhaHash    = "$2a$12$UeHf1Yo1INS.J2enCd.kruqxNqtK4AAx59cFHVE5uHft2oOJtZYvS",
                    Role         = UserRole.Cliente,
                    Status       = UserStatus.Ativo,
                    CriadoEm     = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Usuario
                {
                    Id           = 5,
                    NomeCompleto = "Alberto Fornecedor",
                    Email        = "ziurkelis@gmail.com",
                    Telefone     = "(11) 88888-8888",
                    SenhaHash    = "$2a$12$UeHf1Yo1INS.J2enCd.kruqxNqtK4AAx59cFHVE5uHft2oOJtZYvS",
                    Role         = UserRole.Fornecedor,
                    Status       = UserStatus.Ativo,
                    CriadoEm     = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Usuario
                {
                    Id           = 6,
                    NomeCompleto = "Guilherme Cliente",
                    Email        = "guilherme@gmail.com",
                    Telefone     = "(11) 88888-8888",
                    SenhaHash    = "$2a$12$UeHf1Yo1INS.J2enCd.kruqxNqtK4AAx59cFHVE5uHft2oOJtZYvS",
                    Role         = UserRole.Cliente,
                    Status       = UserStatus.Ativo,
                    CriadoEm     = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Usuario
                {
                    Id           = 7,
                    NomeCompleto = "Guilherme Fornecedor",
                    Email        = "nery@gmail.com",
                    Telefone     = "(11) 88888-8888",
                    SenhaHash    = "$2a$12$UeHf1Yo1INS.J2enCd.kruqxNqtK4AAx59cFHVE5uHft2oOJtZYvS",
                    Role         = UserRole.Fornecedor,
                    Status       = UserStatus.Ativo,
                    CriadoEm     = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Usuario
                {
                    Id           = 8,
                    NomeCompleto = "Josué Cliente",
                    Email        = "josue@gmail.com",
                    Telefone     = "(11) 88888-8888",
                    SenhaHash    = "$2a$12$UeHf1Yo1INS.J2enCd.kruqxNqtK4AAx59cFHVE5uHft2oOJtZYvS",
                    Role         = UserRole.Cliente,
                    Status       = UserStatus.Ativo,
                    CriadoEm     = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Usuario
                {
                    Id           = 9,
                    NomeCompleto = "Josué Fornecedor",
                    Email        = "gumer@gmail.com",
                    Telefone     = "(11) 88888-8888",
                    SenhaHash    = "$2a$12$UeHf1Yo1INS.J2enCd.kruqxNqtK4AAx59cFHVE5uHft2oOJtZYvS",
                    Role         = UserRole.Fornecedor,
                    Status       = UserStatus.Ativo,
                    CriadoEm     = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }
    }
}
