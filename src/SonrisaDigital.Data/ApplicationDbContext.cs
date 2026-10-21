using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Entities;

namespace SonrisaDigital.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<Usuario, Rol, string>(options)
{
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolesPermisos => Set<RolPermiso>();
    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Odontologo> Odontologos => Set<Odontologo>();
    public DbSet<HistoriaClinica> HistoriasClinicas => Set<HistoriaClinica>();
    public DbSet<Cita> Citas => Set<Cita>();
    public DbSet<ConsultaClinica> ConsultasClinicas => Set<ConsultaClinica>();
    public DbSet<PiezaDental> PiezasDentales => Set<PiezaDental>();
    public DbSet<RegistroOdontograma> RegistrosOdontograma => Set<RegistroOdontograma>();
    public DbSet<Tratamiento> Tratamientos => Set<Tratamiento>();
    public DbSet<TratamientoRealizado> TratamientosRealizados => Set<TratamientoRealizado>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<RegistroAuditoria> RegistrosAuditoria => Set<RegistroAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.Property(user => user.Nombre).HasMaxLength(150).IsRequired();
            entity.Property(user => user.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(user => user.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<Rol>(entity =>
        {
            entity.ToTable("Roles");
            entity.Property(role => role.Descripcion).HasMaxLength(500);
            entity.Property(role => role.Estado).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Permiso>(entity =>
        {
            entity.ToTable("Permisos");
            entity.HasKey(permission => permission.Id);
            entity.Property(permission => permission.Nombre).HasMaxLength(150).IsRequired();
            entity.Property(permission => permission.Modulo).HasMaxLength(100).IsRequired();
            entity.HasIndex(permission => permission.Nombre).IsUnique();
        });

        modelBuilder.Entity<RolPermiso>(entity =>
        {
            entity.ToTable("RolesPermisos");
            entity.HasKey(rolePermission => new { rolePermission.RolId, rolePermission.PermisoId });
            entity.HasOne(rolePermission => rolePermission.Rol)
                .WithMany(role => role.RolesPermisos)
                .HasForeignKey(rolePermission => rolePermission.RolId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(rolePermission => rolePermission.Permiso)
                .WithMany(permission => permission.RolesPermisos)
                .HasForeignKey(rolePermission => rolePermission.PermisoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Paciente>(entity =>
        {
            entity.ToTable("Pacientes");
            entity.HasKey(patient => patient.Id);
            entity.Property(patient => patient.Nombres).HasMaxLength(100).IsRequired();
            entity.Property(patient => patient.Apellidos).HasMaxLength(100).IsRequired();
            entity.Property(patient => patient.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(patient => patient.FechaRegistro).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(patient => patient.Identificacion).IsUnique().HasFilter("[Identificacion] IS NOT NULL");
        });

        modelBuilder.Entity<Odontologo>(entity =>
        {
            entity.ToTable("Odontologos");
            entity.HasKey(dentist => dentist.Id);
            entity.Property(dentist => dentist.Nombres).HasMaxLength(100).IsRequired();
            entity.Property(dentist => dentist.Apellidos).HasMaxLength(100).IsRequired();
            entity.Property(dentist => dentist.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(dentist => dentist.FechaRegistro).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(dentist => dentist.NumeroLicencia).IsUnique().HasFilter("[NumeroLicencia] IS NOT NULL");
            entity.HasIndex(dentist => dentist.UsuarioId).IsUnique().HasFilter("[UsuarioId] IS NOT NULL");
            entity.HasOne(dentist => dentist.Usuario)
                .WithOne(user => user.Odontologo)
                .HasForeignKey<Odontologo>(dentist => dentist.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HistoriaClinica>(entity =>
        {
            entity.ToTable("HistoriasClinicas");
            entity.HasKey(history => history.Id);
            entity.HasIndex(history => history.PacienteId).IsUnique();
            entity.HasOne(history => history.Paciente)
                .WithOne(patient => patient.HistoriaClinica)
                .HasForeignKey<HistoriaClinica>(history => history.PacienteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Cita>(entity =>
        {
            entity.ToTable("Citas");
            entity.HasKey(appointment => appointment.Id);
            entity.Property(appointment => appointment.Fecha).IsRequired();
            entity.Property(appointment => appointment.HoraInicio).IsRequired();
            entity.Property(appointment => appointment.HoraFin).IsRequired();
            entity.Property(appointment => appointment.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(appointment => appointment.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(appointment => appointment.Motivo).HasMaxLength(500);
            entity.Property(appointment => appointment.Observaciones).HasMaxLength(1000);
            entity.HasIndex(appointment => new { appointment.OdontologoId, appointment.Fecha, appointment.HoraInicio });
            entity.HasOne(appointment => appointment.Paciente)
                .WithMany(patient => patient.Citas)
                .HasForeignKey(appointment => appointment.PacienteId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(appointment => appointment.Odontologo)
                .WithMany(dentist => dentist.Citas)
                .HasForeignKey(appointment => appointment.OdontologoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ConsultaClinica>(entity =>
        {
            entity.ToTable("ConsultasClinicas");
            entity.HasKey(consultation => consultation.Id);
            entity.Property(consultation => consultation.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(consultation => consultation.Fecha).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(consultation => consultation.CitaId).IsUnique().HasFilter("[CitaId] IS NOT NULL");
            entity.HasOne(consultation => consultation.Paciente)
                .WithMany(patient => patient.ConsultasClinicas)
                .HasForeignKey(consultation => consultation.PacienteId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(consultation => consultation.Odontologo)
                .WithMany(dentist => dentist.ConsultasClinicas)
                .HasForeignKey(consultation => consultation.OdontologoId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(consultation => consultation.Cita)
                .WithOne(appointment => appointment.ConsultaClinica)
                .HasForeignKey<ConsultaClinica>(consultation => consultation.CitaId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(consultation => consultation.HistoriaClinica)
                .WithMany(history => history.ConsultasClinicas)
                .HasForeignKey(consultation => consultation.HistoriaClinicaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PiezaDental>(entity =>
        {
            entity.ToTable("PiezasDentales");
            entity.HasKey(tooth => tooth.Id);
            entity.Property(tooth => tooth.NumeroFDI).HasMaxLength(2).IsRequired();
            entity.Property(tooth => tooth.Nombre).HasMaxLength(100).IsRequired();
            entity.HasIndex(tooth => tooth.NumeroFDI).IsUnique();
        });

        modelBuilder.Entity<RegistroOdontograma>(entity =>
        {
            entity.ToTable("RegistrosOdontograma");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Estado).HasConversion<string>().HasMaxLength(30);
            entity.Property(record => record.FechaModificacion).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(record => new { record.PacienteId, record.PiezaDentalId }).IsUnique();
            entity.HasOne(record => record.Paciente).WithMany(patient => patient.RegistrosOdontograma).HasForeignKey(record => record.PacienteId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(record => record.PiezaDental).WithMany(tooth => tooth.RegistrosOdontograma).HasForeignKey(record => record.PiezaDentalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(record => record.Odontologo).WithMany(dentist => dentist.RegistrosOdontograma).HasForeignKey(record => record.OdontologoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Tratamiento>(entity =>
        {
            entity.ToTable("Tratamientos");
            entity.HasKey(treatment => treatment.Id);
            entity.Property(treatment => treatment.Nombre).HasMaxLength(150).IsRequired();
            entity.Property(treatment => treatment.PrecioBase).HasPrecision(12, 2);
            entity.Property(treatment => treatment.Estado).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(treatment => treatment.Nombre).IsUnique();
        });

        modelBuilder.Entity<TratamientoRealizado>(entity =>
        {
            entity.ToTable("TratamientosRealizados");
            entity.HasKey(treatment => treatment.Id);
            entity.Property(treatment => treatment.Precio).HasPrecision(12, 2);
            entity.Property(treatment => treatment.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(treatment => treatment.Fecha).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(treatment => treatment.Paciente).WithMany(patient => patient.TratamientosRealizados).HasForeignKey(treatment => treatment.PacienteId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(treatment => treatment.Tratamiento).WithMany(catalogTreatment => catalogTreatment.TratamientosRealizados).HasForeignKey(treatment => treatment.TratamientoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(treatment => treatment.Odontologo).WithMany(dentist => dentist.TratamientosRealizados).HasForeignKey(treatment => treatment.OdontologoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(treatment => treatment.PiezaDental).WithMany(tooth => tooth.TratamientosRealizados).HasForeignKey(treatment => treatment.PiezaDentalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(treatment => treatment.ConsultaClinica).WithMany(consultation => consultation.TratamientosRealizados).HasForeignKey(treatment => treatment.ConsultaId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Pago>(entity =>
        {
            entity.ToTable("Pagos");
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.Monto).HasPrecision(12, 2);
            entity.Property(payment => payment.MetodoPago).HasConversion<string>().HasMaxLength(20);
            entity.Property(payment => payment.Fecha).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(payment => payment.UsuarioId).HasMaxLength(450).IsRequired();
            entity.HasIndex(payment => new { payment.TratamientoRealizadoId, payment.Fecha });
            entity.HasIndex(payment => payment.UsuarioId);
            entity.HasOne(payment => payment.TratamientoRealizado)
                .WithMany(treatment => treatment.Pagos)
                .HasForeignKey(payment => payment.TratamientoRealizadoId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(payment => payment.Usuario)
                .WithMany(user => user.Pagos)
                .HasForeignKey(payment => payment.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RegistroAuditoria>(entity =>
        {
            entity.ToTable("RegistrosAuditoria");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Accion).HasMaxLength(50).IsRequired();
            entity.Property(audit => audit.Modulo).HasMaxLength(100).IsRequired();
            entity.Property(audit => audit.Entidad).HasMaxLength(100).IsRequired();
            entity.Property(audit => audit.FechaHora).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(audit => new { audit.UsuarioId, audit.FechaHora });
            entity.HasOne(audit => audit.Usuario)
                .WithMany(user => user.RegistrosAuditoria)
                .HasForeignKey(audit => audit.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}