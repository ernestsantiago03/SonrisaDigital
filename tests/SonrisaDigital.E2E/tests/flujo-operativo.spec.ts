import { expect, test, type Page } from '@playwright/test';

// Credenciales del administrador temporal que crea el script de arranque.
const correoAdmin = process.env.SONRISADIGITAL_E2E_EMAIL;
const claveAdmin = process.env.SONRISADIGITAL_E2E_PASSWORD;

test.describe('Flujo operativo principal', () => {
  test.skip(
    !correoAdmin || !claveAdmin,
    'Defina SONRISADIGITAL_E2E_EMAIL y SONRISADIGITAL_E2E_PASSWORD para ejecutar las pruebas E2E.'
  );

  test('login, paciente, cita, tratamiento y pago', async ({ page }) => {
    // Un sufijo único evita choques si la prueba se ejecuta varias veces sobre
    // una base que no se haya eliminado.
    const sufijo = Date.now().toString().slice(-8);
    const nombrePaciente = `E2E Paciente ${sufijo}`;
    const apellidoPaciente = 'Automatico';
    const nombreOdontologo = `E2E Odontologo ${sufijo}`;
    const nombreTratamiento = `E2E Limpieza ${sufijo}`;
    const fechaCita = siguienteDiaLaborable();
    const fechaTratamiento = fechaLocal(new Date());

    await iniciarSesion(page, correoAdmin!, claveAdmin!);
    await expect(page.getByRole('heading', { name: 'Inicio', exact: true })).toBeVisible();

    // ---------------------------------------------------------------------
    // 1. Odontólogo: la cita y el tratamiento necesitan uno activo.
    // ---------------------------------------------------------------------
    await irA(page, '/odontologo/nuevo');
    await page.locator('#first-name').fill('E2E');
    await page.locator('#last-name').fill(`Odontologo ${sufijo}`);
    await page.locator('#license-number').fill(`E2E-${sufijo}`);
    await page.locator('#specialty').selectOption({ label: 'Odontología General' });
    await page.locator('#phone').fill('809-555-0101');
    await page.locator('#email').fill(`odontologo.${sufijo}@example.com`);
    await page.getByRole('button', { name: 'Guardar odontólogo' }).click();

    await expect(page).toHaveURL(/\/odontologo\/\d+$/);
    await expect(page.getByRole('heading', { name: `${nombreOdontologo} 🦷` })).toBeVisible();

    // ---------------------------------------------------------------------
    // 2. Paciente: se crea desde el formulario real de la aplicación.
    // ---------------------------------------------------------------------
    await irA(page, '/pacientes/nuevo');
    await page.locator('#first-name').fill(nombrePaciente);
    await page.locator('#last-name').fill(apellidoPaciente);
    await page.locator('#identification').fill(`E2E-${sufijo}`);
    await page.locator('#birth-date').fill('1990-01-15');
    await page.locator('#sex').selectOption('Femenino');
    await page.locator('#tipo-sangre').selectOption('O+');
    await page.locator('#phone').fill('809-555-0202');
    await page.locator('#email').fill(`paciente.${sufijo}@example.com`);
    await page.locator('#address').fill('Calle E2E 123');
    await page.getByRole('button', { name: 'Guardar paciente' }).click();

    await expect(page).toHaveURL(/\/pacientes\/\d+$/);
    await expect(
      page.getByRole('heading', { name: `${nombrePaciente} ${apellidoPaciente}` })
    ).toBeVisible();

    // ---------------------------------------------------------------------
    // 3. Cita: se agenda para el siguiente día laborable.
    // ---------------------------------------------------------------------
    await irA(page, '/cita/nuevo');
    await page.locator('#patient-select').selectOption({
      label: `${nombrePaciente} ${apellidoPaciente}`
    });
    await page.locator('#dentist-select').selectOption({ label: nombreOdontologo });
    await page.locator('#date').fill(fechaCita);
    await page.locator('#start-time').selectOption('09:00');
    await page.locator('#reason').selectOption({
      label: 'Limpieza dental profesional (profilaxis)'
    });
    await page.getByRole('button', { name: 'Guardar cita' }).click();

    await expect(page).toHaveURL(/\/cita\/\d+$/);
    await expect(page.getByRole('heading', { name: /Cita #\d+/ })).toBeVisible();

    // ---------------------------------------------------------------------
    // 4. Tratamiento: primero el catálogo y después el realizado al paciente.
    // ---------------------------------------------------------------------
    await irA(page, '/tratamientos');
    await page.getByRole('button', { name: 'Nuevo tratamiento' }).click();
    await expect(page.locator('#catalogo-nombre')).toBeVisible();
    await page.locator('#catalogo-nombre').fill(nombreTratamiento);
    await page.locator('#catalogo-descripcion').fill('Tratamiento creado por la prueba E2E.');
    await page.locator('#catalogo-precio').fill('1500');
    await page.locator('.modal-card').getByRole('button', { name: 'Guardar', exact: true }).click();
    await expect(page.getByText('Tratamiento guardado correctamente.')).toBeVisible();

    await page.getByRole('tab', { name: 'Realizados' }).click();
    await page.getByRole('button', { name: 'Registrar tratamiento' }).click();
    await expect(page.locator('#realizado-paciente')).toBeVisible();

    // La base temporal solo contiene los datos creados por esta prueba.
    await expect(page.locator('#realizado-paciente option')).toHaveCount(2);
    await expect(page.locator('#realizado-odontologo option')).toHaveCount(2);
    await expect(page.locator('#realizado-tratamiento option')).toHaveCount(2);

    await page.locator('#realizado-paciente').selectOption({ index: 1 });
    await page.locator('#realizado-odontologo').selectOption({ index: 1 });
    await page.locator('#realizado-tratamiento').selectOption({ index: 1 });
    await page.locator('#realizado-fecha').fill(fechaTratamiento);
    await page.locator('#realizado-precio').fill('1500');
    await page.locator('.modal-card').getByRole('button', { name: 'Guardar', exact: true }).click();
    await expect(page.getByText('Tratamiento realizado guardado correctamente.')).toBeVisible();
    await expect(
      page.getByRole('cell', { name: `${nombrePaciente} ${apellidoPaciente}`, exact: true })
    ).toBeVisible();

    // ---------------------------------------------------------------------
    // 5. Pago: se registra una cuota parcial y se comprueba el saldo.
    // ---------------------------------------------------------------------
    await irA(page, '/pagos');
    await page.locator('#payment-search').fill(nombrePaciente);
    await page.getByRole('button', { name: 'Buscar' }).click();

    // La página tiene dos tablas: tratamientos e historial. La fila que se
    // necesita está en la primera.
    const filaPago = page
      .locator('section.table-panel')
      .first()
      .getByRole('row', { name: new RegExp(`${nombrePaciente} ${apellidoPaciente}`) });
    await expect(filaPago).toBeVisible();
    await filaPago.getByRole('button', { name: '+ Pago' }).click();
    await expect(page.locator('#pay-amount')).toBeVisible();
    await page.locator('#pay-amount').fill('500');
    await page.locator('#pay-method').selectOption('Tarjeta');
    await page.locator('.modal-card').getByRole('button', { name: 'Registrar pago', exact: true }).click();

    await expect(
      page.getByText(
        `Pago registrado correctamente para ${nombrePaciente} ${apellidoPaciente}.`
      )
    ).toBeVisible();
    await expect(filaPago.getByText('Pago parcial')).toBeVisible();

    await filaPago.getByRole('link', { name: 'Ver' }).click();
    await expect(page).toHaveURL(/\/pagos\/\d+$/);
    await expect(
      page.getByRole('heading', { name: `${nombrePaciente} ${apellidoPaciente}` })
    ).toBeVisible();
    await expect(page.getByRole('cell', { name: 'RD$500' })).toBeVisible();
  });
});

async function iniciarSesion(page: Page, correo: string, clave: string): Promise<void> {
  await page.goto('/login');
  await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).toBeVisible();
  await page.locator('#email').fill(correo);
  await page.locator('#password').fill(clave);
  await page.getByRole('button', { name: 'Ingresar' }).click();
  await expect(page).toHaveURL(/\/$/);
  await esperarBlazor(page);
}

// Navega a una página interactiva y espera a que Blazor haya conectado el
// circuito. Sin esta espera, los campos aceptan texto pero el modelo del
// servidor sigue vacío y el formulario no se guarda.
async function irA(page: Page, ruta: string): Promise<void> {
  await page.goto(ruta);
  await esperarBlazor(page);
}

async function esperarBlazor(page: Page): Promise<void> {
  await page.waitForFunction(
    () => Boolean((window as any).Blazor?.reconnect),
    undefined,
    { timeout: 20_000 }
  );
  // Margen breve para que Blazor termine de enlazar los eventos del formulario.
  await page.waitForTimeout(300);
}

// Devuelve la fecha en formato YYYY-MM-DD usando la zona horaria del equipo.
function fechaLocal(fecha: Date): string {
  const anio = fecha.getFullYear();
  const mes = String(fecha.getMonth() + 1).padStart(2, '0');
  const dia = String(fecha.getDate()).padStart(2, '0');
  return `${anio}-${mes}-${dia}`;
}

// La clínica agenda de lunes a viernes; se salta el fin de semana.
function siguienteDiaLaborable(): string {
  const fecha = new Date();
  fecha.setDate(fecha.getDate() + 1);
  while (fecha.getDay() === 0 || fecha.getDay() === 6) {
    fecha.setDate(fecha.getDate() + 1);
  }
  return fechaLocal(fecha);
}
