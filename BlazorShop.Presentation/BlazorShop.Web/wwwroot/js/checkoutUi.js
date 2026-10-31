const dialogs = new WeakMap();

export function setDialogOpen(dialog, open, closeOnBackdrop, reference) {
    if (!dialog?.isConnected) return;
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
        state.keydown = event => {
            if (event.key !== 'Tab' || event.altKey || event.ctrlKey || event.metaKey) return;
            const controls = [...dialog.querySelectorAll('a[href], button, input, select, textarea, [tabindex]')]
                .filter(element => element.tabIndex >= 0 && !element.matches(':disabled, [type="hidden"]') && element.getClientRects().length > 0);
            const first = controls[0];
            const last = controls.at(-1);
            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        };
        dialog.addEventListener('cancel', state.cancel);
        dialog.addEventListener('click', state.click);
        dialog.addEventListener('keydown', state.keydown);
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
    dialog.removeEventListener('keydown', state.keydown);
    if (dialog.open) dialog.close();
    if (state.invoker?.isConnected) state.invoker.focus();
    dialogs.delete(dialog);
}

