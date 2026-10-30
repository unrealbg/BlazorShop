const shells = new WeakMap();
const dialogs = new WeakMap();

export function initializeShell(shell) {
    if (shells.has(shell)) return;
    const onClick = event => {
        if (event.target.closest('.bs-workspace-header__mobile-panel a')) {
            const menu = shell.querySelector('.bs-workspace-header__menu');
            if (menu) menu.open = false;
        }
    };
    shell.addEventListener('click', onClick);
    shells.set(shell, onClick);
}

export function disposeShell(shell) {
    const listener = shells.get(shell);
    if (listener) shell.removeEventListener('click', listener);
    shells.delete(shell);
}

export function setDialogOpen(dialog, open, closeOnBackdrop, reference) {
    let state = dialogs.get(dialog);
    if (!state) {
        state = { reference, closeOnBackdrop };
        state.cancel = event => {
            event.preventDefault();
            state.reference.invokeMethodAsync('CloseAsync');
        };
        state.click = event => {
            if (event.target !== dialog || !state.closeOnBackdrop) return;
            const box = dialog.getBoundingClientRect();
            if (event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom) {
                state.reference.invokeMethodAsync('CloseAsync');
            }
        };
        dialog.addEventListener('cancel', state.cancel);
        dialog.addEventListener('click', state.click);
        dialogs.set(dialog, state);
    }
    state.closeOnBackdrop = closeOnBackdrop;
    if (open && !dialog.open) {
        state.invoker = document.activeElement;
        dialog.showModal();
        const firstField = dialog.querySelector('[autofocus], input:not([type="hidden"]):not([disabled]), select:not([disabled]), textarea:not([disabled])');
        if (firstField) firstField.focus();
    } else if (!open && dialog.open) {
        dialog.close();
        if (state.invoker?.isConnected) state.invoker.focus();
    }
}

export function disposeDialog(dialog) {
    const state = dialogs.get(dialog);
    if (!state) return;
    dialog.removeEventListener('cancel', state.cancel);
    dialog.removeEventListener('click', state.click);
    if (dialog.open) dialog.close();
    if (state.invoker?.isConnected) state.invoker.focus();
    dialogs.delete(dialog);
}
