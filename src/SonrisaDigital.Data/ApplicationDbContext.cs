using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SonrisaDigital.Data.Entities;
using SonrisaDigital.Data.Tenancy;

namespace SonrisaDigital.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantProvider tenant)
    : IdentityDbContext<Usuario, Rol, string>(options)
{
    private readonly ITenantProvider _tenant = tenant;

    // Clínica activa. Si no hay clínica, el filtro devuelve cero filas (cierre por defecto).
    private int? ClinicaActual => _tenant.ClinicaId;

    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolesPermisos => Set<RolPermiso>();
    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Odontologo> Odontologos => Set<Odontologo>();
    public DbSet<HistoriaClinica> HistoriasClinicas => Set<HistoriaClinica>();
    public DbSet<Cita> Citas => Set<Cita>();
    public DbSet<ConsultaClinica> ConsultasClinicas => Set<ConsultaClinica>();
    public DbSet<PiezaDental> PiezasDentales => Set<PiezaDental>();
    public DbSet<RegistroOdontograma> RegistrosOdontograma => Set<RegistroOdontograma>();
    public DbSet<HistorialOdontograma> HistorialesOdontograma => Set<HistorialOdontograma>();
    public DbSet<ArchivoPaciente> ArchivosPaciente => Set<ArchivoPaciente>();
    public DbSet<Tratamiento> Tratamientos => Set<Tratamiento>();
    public DbSet<TratamientoRealizado> TratamientosRealizados => Set<TratamientoRealizado>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<RegistroAuditoria> RegistrosAuditoria => Set<RegistroAuditoria>();
    public DbSet<Clinica> Clinicas => Set<Clinica>();
    public DbSet<PagoSuscripcion> PagosSuscripcion => Set<PagoSuscripcion>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AplicarClinicaAlGuardar();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AplicarClinicaAlGuardar();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Completa la clínica de los registros nuevos con la clínica activa y bloquea cualquier
    /// intento de guardar un registro en otra clínica o de cambiar su clínica.
    /// </summary>
    private void AplicarClinicaAlGuardar()
    {
        foreach (var entrada in ChangeTracker.Entries<IPerteneceAClinica>())
        {
            if (entrada.State == EntityState.Added)
            {
                if (entrada.Entity.ClinicaId == 0)
                {
                    if (ClinicaActual is null)
                    {
                        throw new InvalidOperationException("No hay clínica en la sesión; no se puede guardar el registro.");
                    }

                    entrada.Entity.ClinicaId = ClinicaActual.Value;
                }
                else if (!_tenant.EsAdministradorGlobal && entrada.Entity.ClinicaId != ClinicaActual)
                {
                    throw new InvalidOperationException("No se puede guardar un registro de otra clínica.");
                }
            }
            else if (entrada.State == EntityState.Modified
                && entrada.Property(nameof(IPerteneceAClinica.ClinicaId)).IsModified)
            {
                throw new InvalidOperationException("La clínica de un registro no se puede cambiar.");
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Clinica>(entity =>
        {
            entity.ToTable("Clinicas");
            entity.Property(clinic => clinic.Nombre).HasMaxLength(150).IsRequired();
            entity.Property(clinic => clinic.Codigo).HasMaxLength(30).IsRequired();
            entity.HasIndex(clinic => clinic.Codigo).IsUnique();
            entity.Property(clinic => clinic.Rnc).HasMaxLength(30);
            entity.Property(clinic => clinic.Telefono).HasMaxLength(30);
            entity.Property(clinic => clinic.Correo).HasMaxLength(150);
            entity.Property(clinic => clinic.Direccion).HasMaxLength(300);
            entity.Property(clinic => clinic.Horario).HasMaxLength(150);
            entity.Property(clinic => clinic.Eslogan).HasMaxLength(300);
            entity.Property(clinic => clinic.LogoUrl).HasMaxLength(300);
            entity.Property(clinic => clinic.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(clinic => clinic.MotivoSuspension).HasConversion<string>().HasMaxLength(30);
            entity.Property(clinic => clinic.Plan).HasMaxLength(30);
        });

        modelBuilder.Entity<PagoSuscripcion>(entity =>
        {
            entity.ToTable("PagosSuscripcion");
            entity.Property(payment => payment.Monto).HasPrecision(12, 2);
            entity.Property(payment => payment.Moneda).HasMaxLength(3).IsRequired();
            entity.Property(payment => payment.Metodo).HasConversion<string>().HasMaxLength(20);
            entity.Property(payment => payment.Referencia).HasMaxLength(100);
            entity.Property(payment => payment.Notas).HasMaxLength(500);
            entity.Property(payment => payment.RegistradoPorId).HasMaxLength(450).IsRequired();
            entity.HasOne(payment => payment.Clinica)
                .WithMany(clinic => clinic.PagosSuscripcion)
                .HasForeignKey(payment => payment.ClinicaId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(payment => payment.RegistradoPor)
                .WithMany(user => user.PagosSuscripcionRegistrados)
                .HasForeignKey(payment => payment.RegistradoPorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(payment => new { payment.ClinicaId, payment.FechaPago });
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.Property(user => user.Nombre).HasMaxLength(150).IsRequired();
            entity.HasOne(user => user.Clinica)
                .WithMany(clinic => clinic.Usuarios)
                .HasForeignKey(user => user.ClinicaId)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.HasAlternateKey(patient => new { patient.ClinicaId, patient.Id });
            entity.Property(patient => patient.Nombres).HasMaxLength(100).IsRequired();
            entity.Property(patient => patient.Apellidos).HasMaxLength(100).IsRequired();
            entity.Property(patient => patient.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(patient => patient.FechaRegistro).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(patient => patient.ClinicaId);
            entity.HasIndex(patient => new { patient.ClinicaId, patient.Identificacion }).IsUnique().HasFilter("[Identificacion] IS NOT NULL");
            entity.HasQueryFilter(patient => patient.ClinicaId == ClinicaActual);
        });

        modelBuilder.Entity<Odontologo>(entity =>
        {
            entity.ToTable("Odontologos");
            entity.HasKey(dentist => dentist.Id);
            entity.HasAlternateKey(dentist => new { dentist.ClinicaId, dentist.Id });
            entity.Property(dentist => dentist.Nombres).HasMaxLength(100).IsRequired();
            entity.Property(dentist => dentist.Apellidos).HasMaxLength(100).IsRequired();
            entity.Property(dentist => dentist.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(dentist => dentist.FechaRegistro).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(dentist => dentist.ClinicaId);
            entity.HasIndex(dentist => new { dentist.ClinicaId, dentist.NumeroLicencia }).IsUnique().HasFilter("[NumeroLicencia] IS NOT NULL");
            entity.HasIndex(dentist => dentist.UsuarioId).IsUnique().HasFilter("[UsuarioId] IS NOT NULL");
            entity.HasOne(dentist => dentist.Usuario)
                .WithOne(user => user.Odontologo)
                .HasForeignKey<Odontologo>(dentist => dentist.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(dentist => dentist.ClinicaId == ClinicaActual);
        });

        modelBuilder.Entity<HistoriaClinica>(entity =>
        {
            entity.ToTable("HistoriasClinicas");
            entity.HasKey(history => history.Id);
            entity.HasAlternateKey(history => new { history.ClinicaId, history.Id });
            entity.HasIndex(history => history.ClinicaId);
            entity.HasIndex(history => history.PacienteId).IsUnique();
            entity.HasOne(history => history.Paciente)
                .WithOne(patient => patient.HistoriaClinica)
                .HasForeignKey<HistoriaClinica>(history => new { history.ClinicaId, history.PacienteId })
                .HasPrincipalKey<Paciente>(patient => new { patient.ClinicaId, patient.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(history => history.ClinicaId == ClinicaActual);
        });

        modelBuilder.Entity<Cita>(entity =>
        {
            entity.ToTable("Citas");
            entity.HasKey(appointment => appointment.Id);
            entity.HasAlternateKey(appointment => new { appointment.ClinicaId, appointment.Id });
            entity.Property(appointment => appointment.Fecha).IsRequired();
            entity.Property(appointment => appointment.HoraInicio).IsRequired();
            entity.Property(appointment => appointment.HoraFin).IsRequired();
            entity.Property(appointment => appointment.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(appointment => appointment.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(appointment => appointment.Motivo).HasMaxLength(500);
            entity.Property(appointment => appointment.Observaciones).HasMaxLength(1000);
            entity.HasIndex(appointment => new { appointment.ClinicaId, appointment.OdontologoId, appointment.Fecha, appointment.HoraInicio });
            // Permite localizar rápido los choques de horario de un paciente en una fecha.
            entity.HasIndex(appointment => new { appointment.ClinicaId, appointment.PacienteId, appointment.Fecha, appointment.HoraInicio });
            entity.HasOne(appointment => appointment.Paciente)
                .WithMany(patient => patient.Citas)
                .HasForeignKey(appointment => new { appointment.ClinicaId, appointment.PacienteId })
                .HasPrincipalKey(patient => new { patient.ClinicaId, patient.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(appointment => appointment.Odontologo)
                .WithMany(dentist => dentist.Citas)
                .HasForeignKey(appointment => new { appointment.ClinicaId, appointment.OdontologoId })
                .HasPrincipalKey(dentist => new { dentist.ClinicaId, dentist.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(appointment => appointment.ClinicaId == ClinicaActual);
        });

        modelBuilder.Entity<ConsultaClinica>(entity =>
        {
            entity.ToTable("ConsultasClinicas");
            entity.HasKey(consultation => consultation.Id);
            entity.HasAlternateKey(consultation => new { consultation.ClinicaId, consultation.Id });
            entity.Property(consultation => consultation.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(consultation => consultation.Fecha).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(consultation => consultation.ClinicaId);
            entity.HasIndex(consultation => consultation.CitaId).IsUnique().HasFilter("[CitaId] IS NOT NULL");
            entity.HasOne(consultation => consultation.Paciente)
                .WithMany(patient => patient.ConsultasClinicas)
                .HasForeignKey(consultation => new { consultation.ClinicaId, consultation.PacienteId })
                .HasPrincipalKey(patient => new { patient.ClinicaId, patient.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(consultation => consultation.Odontologo)
                .WithMany(dentist => dentist.ConsultasClinicas)
                .HasForeignKey(consultation => new { consultation.ClinicaId, consultation.OdontologoId })
                .HasPrincipalKey(dentist => new { dentist.ClinicaId, dentist.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(consultation => consultation.Cita)
                .WithOne(appointment => appointment.ConsultaClinica)
                .HasForeignKey<ConsultaClinica>(consultation => new { consultation.ClinicaId, consultation.CitaId })
                .HasPrincipalKey<Cita>(appointment => new { appointment.ClinicaId, appointment.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(consultation => consultation.HistoriaClinica)
                .WithMany(history => history.ConsultasClinicas)
                .HasForeignKey(consultation => new { consultation.ClinicaId, consultation.HistoriaClinicaId })
                .HasPrincipalKey(history => new { history.ClinicaId, history.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(consultation => consultation.ClinicaId == ClinicaActual);
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
            entity.HasIndex(record => record.ClinicaId);
            entity.HasIndex(record => new { record.PacienteId, record.PiezaDentalId }).IsUnique();
            entity.HasOne(record => record.Paciente)
                .WithMany(patient => patient.RegistrosOdontograma)
                .HasForeignKey(record => new { record.ClinicaId, record.PacienteId })
                .HasPrincipalKey(patient => new { patient.ClinicaId, patient.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(record => record.PiezaDental).WithMany(tooth => tooth.RegistrosOdontograma).HasForeignKey(record => record.PiezaDentalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(record => record.Odontologo)
                .WithMany(dentist => dentist.RegistrosOdontograma)
                .HasForeignKey(record => new { record.ClinicaId, record.OdontologoId })
                .HasPrincipalKey(dentist => new { dentist.ClinicaId, dentist.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(record => record.ClinicaId == ClinicaActual);
        });

        modelBuilder.Entity<ArchivoPaciente>(entity =>
        {
            entity.ToTable("ArchivosPaciente");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.NombreOriginal).HasMaxLength(255).IsRequired();
            entity.Property(item => item.ClaveArchivo).HasMaxLength(300).IsRequired();
            entity.Property(item => item.ClaveMiniatura).HasMaxLength(300).IsRequired();
            entity.Property(item => item.TipoContenido).HasMaxLength(30).IsRequired();
            entity.Property(item => item.Descripcion).HasMaxLength(300);
            entity.Property(item => item.UsuarioId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.FechaSubida).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(item => item.ClinicaId);
            entity.HasIndex(item => new { item.ClinicaId, item.PacienteId, item.FechaEstudio });
            entity.HasOne(item => item.Paciente)
                .WithMany()
                .HasForeignKey(item => new { item.ClinicaId, item.PacienteId })
                .HasPrincipalKey(patient => new { patient.ClinicaId, patient.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Usuario)
                .WithMany()
                .HasForeignKey(item => item.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.OcultadoPor)
                .WithMany()
                .HasForeignKey(item => item.OcultadoPorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(item => item.ClinicaId == ClinicaActual);
        });

        modelBuilder.Entity<HistorialOdontograma>(entity =>
        {
            entity.ToTable("HistorialOdontograma");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EstadoAnterior).HasConversion<string>().HasMaxLength(30);
            entity.Property(item => item.EstadoNuevo).HasConversion<string>().HasMaxLength(30);
            entity.Property(item => item.FechaHora).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(item => item.ClinicaId);
            entity.HasIndex(item => new { item.ClinicaId, item.PacienteId, item.PiezaDentalId, item.FechaHora });
            entity.HasOne(item => item.Paciente)
                .WithMany()
                .HasForeignKey(item => new { item.ClinicaId, item.PacienteId })
                .HasPrincipalKey(patient => new { patient.ClinicaId, patient.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PiezaDental)
                .WithMany()
                .HasForeignKey(item => item.PiezaDentalId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Odontologo)
                .WithMany()
                .HasForeignKey(item => new { item.ClinicaId, item.OdontologoId })
                .HasPrincipalKey(dentist => new { dentist.ClinicaId, dentist.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(item => item.ClinicaId == ClinicaActual);
        });

        modelBuilder.Entity<Tratamiento>(entity =>
        {
            entity.ToTable("Tratamientos");
            entity.HasKey(treatment => treatment.Id);
            entity.HasAlternateKey(treatment => new { treatment.ClinicaId, treatment.Id });
            entity.Property(treatment => treatment.Nombre).HasMaxLength(150).IsRequired();
            entity.Property(treatment => treatment.PrecioBase).HasPrecision(12, 2);
            entity.Property(treatment => treatment.Estado).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(treatment => treatment.ClinicaId);
            entity.HasIndex(treatment => new { treatment.ClinicaId, treatment.Nombre }).IsUnique();
            entity.HasQueryFilter(treatment => treatment.ClinicaId == ClinicaActual);
        });

        modelBuilder.Entity<TratamientoRealizado>(entity =>
        {
            entity.ToTable("TratamientosRealizados");
            entity.HasKey(treatment => treatment.Id);
            entity.HasAlternateKey(treatment => new { treatment.ClinicaId, treatment.Id });
            entity.Property(treatment => treatment.Precio).HasPrecision(12, 2);
            entity.Property(treatment => treatment.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(treatment => treatment.Fecha).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(treatment => treatment.ClinicaId);
            entity.HasOne(treatment => treatment.Paciente)
                .WithMany(patient => patient.TratamientosRealizados)
                .HasForeignKey(treatment => new { treatment.ClinicaId, treatment.PacienteId })
                .HasPrincipalKey(patient => new { patient.ClinicaId, patient.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(treatment => treatment.Tratamiento)
                .WithMany(catalogTreatment => catalogTreatment.TratamientosRealizados)
                .HasForeignKey(treatment => new { treatment.ClinicaId, treatment.TratamientoId })
                .HasPrincipalKey(catalogTreatment => new { catalogTreatment.ClinicaId, catalogTreatment.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(treatment => treatment.Odontologo)
                .WithMany(dentist => dentist.TratamientosRealizados)
                .HasForeignKey(treatment => new { treatment.ClinicaId, treatment.OdontologoId })
                .HasPrincipalKey(dentist => new { dentist.ClinicaId, dentist.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(treatment => treatment.PiezaDental).WithMany(tooth => tooth.TratamientosRealizados).HasForeignKey(treatment => treatment.PiezaDentalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(treatment => treatment.ConsultaClinica)
                .WithMany(consultation => consultation.TratamientosRealizados)
                .HasForeignKey(treatment => new { treatment.ClinicaId, treatment.ConsultaId })
                .HasPrincipalKey(consultation => new { consultation.ClinicaId, consultation.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(treatment => treatment.ClinicaId == ClinicaActual);
        });

        modelBuilder.Entity<Pago>(entity =>
        {
            entity.ToTable("Pagos");
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.Monto).HasPrecision(12, 2);
            entity.Property(payment => payment.MetodoPago).HasConversion<string>().HasMaxLength(20);
            entity.Property(payment => payment.Fecha).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(payment => payment.UsuarioId).HasMaxLength(450).IsRequired();
            entity.HasIndex(payment => payment.ClinicaId);
            entity.HasIndex(payment => new { payment.ClinicaId, payment.Fecha });
            entity.HasIndex(payment => new { payment.TratamientoRealizadoId, payment.Fecha });
            entity.HasIndex(payment => payment.UsuarioId);
            entity.HasOne(payment => payment.TratamientoRealizado)
                .WithMany(treatment => treatment.Pagos)
                .HasForeignKey(payment => new { payment.ClinicaId, payment.TratamientoRealizadoId })
                .HasPrincipalKey(treatment => new { treatment.ClinicaId, treatment.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(payment => payment.Usuario)
                .WithMany(user => user.Pagos)
                .HasForeignKey(payment => payment.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(payment => payment.ClinicaId == ClinicaActual);
        });

        modelBuilder.Entity<RegistroAuditoria>(entity =>
        {
            entity.ToTable("RegistrosAuditoria");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Accion).HasMaxLength(50).IsRequired();
            entity.Property(audit => audit.Modulo).HasMaxLength(100).IsRequired();
            entity.Property(audit => audit.Entidad).HasMaxLength(100).IsRequired();
            entity.Property(audit => audit.FechaHora).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(audit => audit.ClinicaId);
            entity.HasIndex(audit => new { audit.UsuarioId, audit.FechaHora });
            entity.HasIndex(audit => new { audit.ClinicaId, audit.FechaHora });
            entity.HasOne(audit => audit.Usuario)
                .WithMany(user => user.RegistrosAuditoria)
                .HasForeignKey(audit => audit.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(audit => audit.ClinicaId == ClinicaActual);
        });
    }
}
