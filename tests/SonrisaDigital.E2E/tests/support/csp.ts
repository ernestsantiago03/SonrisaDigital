import { test as base, type Page } from '@playwright/test';
import { appendFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';

// Cada violación de Content-Security-Policy que registra el navegador (tanto la política
// activa como la candidata en Report-Only) se guarda aquí para revisarla al final de la corrida.
const carpetaResultados = join(process.cwd(), 'test-results');
const archivoViolaciones = join(carpetaResultados, 'csp-violaciones.jsonl');

const patronCsp = /Content[ -]Security[ -]Policy|violates the following/i;

export function registrarViolacionesCsp(page: Page, origen: string): void {
  const vistas = new Set<string>();

  page.on('console', (mensaje) => {
    const texto = mensaje.text();
    if (!patronCsp.test(texto) || vistas.has(texto)) {
      return;
    }

    vistas.add(texto);
    mkdirSync(carpetaResultados, { recursive: true });
    appendFileSync(
      archivoViolaciones,
      JSON.stringify({ origen, url: page.url(), tipo: mensaje.type(), mensaje: texto }) + '\n'
    );
  });
}

export const test = base.extend<{ monitorCsp: void }>({
  monitorCsp: [
    async ({ page }, use, testInfo) => {
      registrarViolacionesCsp(page, testInfo.title);
      await use();
    },
    { auto: true }
  ]
});
