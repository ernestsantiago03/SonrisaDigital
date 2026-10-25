// El script se carga como módulo después de que el <dialog> ya está en el DOM.
const reconnectModal = document.getElementById("components-reconnect-modal");

if (reconnectModal) {
    const retryButton = document.getElementById("components-reconnect-button");
    const resumeButton = document.getElementById("components-resume-button");

    reconnectModal.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged);

    if (retryButton) {
        retryButton.addEventListener("click", retry);
    }

    if (resumeButton) {
        resumeButton.addEventListener("click", resume);
    }
}

function handleReconnectStateChanged(event) {
    if (!reconnectModal) {
        return;
    }

    if (event.detail.state === "show" || event.detail.state === "paused") {
        if (!reconnectModal.open) {
            reconnectModal.showModal();
        }
    } else if (event.detail.state === "hide") {
        document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);

        if (reconnectModal.open) {
            reconnectModal.close();
        }
    } else if (event.detail.state === "failed") {
        if (!reconnectModal.open) {
            reconnectModal.showModal();
        }

        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    } else if (event.detail.state === "rejected") {
        // El servidor ya no conserva el circuito: recargar reconstruye la sesión.
        location.reload();
    }
}

async function retry() {
    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);

    try {
        // true: reconectado; false: se alcanzó el servidor pero rechazó el circuito.
        const successful = await Blazor.reconnect();

        if (!successful) {
            const resumeSuccessful = await Blazor.resumeCircuit();

            if (!resumeSuccessful) {
                location.reload();
            } else {
                reconnectModal.close();
            }
        }
    } catch {
        // El servidor sigue sin responder; se reintenta al volver a la pestaña.
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    }
}

async function resume() {
    try {
        const successful = await Blazor.resumeCircuit();

        if (!successful) {
            location.reload();
        }
    } catch {
        reconnectModal.classList.replace("components-reconnect-paused", "components-reconnect-resume-failed");
    }
}

async function retryWhenDocumentBecomesVisible() {
    if (document.visibilityState === "visible") {
        await retry();
    }
}
