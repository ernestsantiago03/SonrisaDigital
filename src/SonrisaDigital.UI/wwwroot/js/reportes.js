// Descarga de reportes en PDF.
// El servidor genera el documento con QuestPDF y lo envía como base64;
// aquí se reconstruye el archivo binario para que el navegador lo descargue.
window.sonrisaDescargarPdf = function (nombreArchivo, base64) {
    const bytes = Uint8Array.from(atob(base64), caracter => caracter.charCodeAt(0));
    const blob = new Blob([bytes], { type: "application/pdf" });
    const url = URL.createObjectURL(blob);
    const enlace = document.createElement("a");

    enlace.href = url;
    enlace.download = nombreArchivo;
    document.body.appendChild(enlace);
    enlace.click();
    enlace.remove();

    // Se espera un instante antes de liberar la URL para no interrumpir
    // la descarga en navegadores que la procesan de forma asíncrona.
    setTimeout(() => URL.revokeObjectURL(url), 1000);
};
