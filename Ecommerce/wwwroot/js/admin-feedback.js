(() => {
    "use strict";
    const dialog = document.getElementById("adminFeedback");
    if (!dialog) return;
    const title = dialog.querySelector("h2");
    const message = dialog.querySelector("p");
    const symbol = dialog.querySelector(".admin-feedback-symbol");
    const ok = dialog.querySelector(".admin-feedback-ok");
    const cancel = dialog.querySelector(".admin-feedback-cancel");
    const ar = document.documentElement.dir === "rtl";
    const copy = ar ? {
        processing: ["جارٍ المعالجة", "يرجى عدم إغلاق الصفحة أو إعادة إرسال الطلب."],
        success: ["تم بنجاح", "تم حفظ العملية بنجاح."],
        error: ["تعذر إكمال العملية", "تعذر تأكيد نجاح العملية. تحقق من النتيجة قبل إعادة المحاولة."],
        warning: ["تحقق من البيانات", "يرجى مراجعة الحقول المطلوبة."],
        confirm: ["تأكيد العملية", "هل تريد المتابعة؟"],
        proceed: "متابعة", cancel: "إلغاء", ok: "حسناً"
    } : {
        processing: ["Processing…", "Please do not close the page or submit again."],
        success: ["Saved successfully", "The operation was completed."],
        error: ["Unable to complete", "The operation could not be confirmed. Check the result before retrying."],
        warning: ["Check your details", "Please review the highlighted fields."],
        confirm: ["Confirm operation", "Do you want to continue?"],
        proceed: "Continue", cancel: "Cancel", ok: "OK"
    };
    let pending = false;
    let activeForm = null;
    let resolveClose = null;
    let previousFocus = null;
    function show(state, text, heading) {
        if (!copy[state]) state = "warning";
        if (!dialog.open) {
            previousFocus = document.activeElement;
            dialog.returnValue = "";
        }
        dialog.dataset.state = state;
        title.textContent = heading || copy[state][0];
        message.textContent = text || copy[state][1];
        symbol.textContent = ({success:"✓", error:"!", warning:"!"})[state] || "";
        ok.textContent = state === "confirm" ? copy.proceed : copy.ok;
        ok.hidden = state === "processing";
        cancel.textContent = copy.cancel;
        cancel.hidden = state !== "confirm";
        dialog.setAttribute("aria-busy", String(state === "processing"));
        if (!dialog.open) dialog.showModal();
        if (!cancel.hidden) cancel.focus();
        else if (!ok.hidden) ok.focus();
        if (state === "processing") return;
        return new Promise(resolve => { resolveClose = resolve; });
    }
    ok.addEventListener("click", () => dialog.close("confirmed"));
    cancel.addEventListener("click", () => dialog.close("cancelled"));
    dialog.addEventListener("cancel", e => {
        if (pending || dialog.dataset.state === "processing") e.preventDefault();
    });
    dialog.addEventListener("close", () => {
        if (resolveClose) { const done = resolveClose; resolveClose = null; done(dialog.returnValue === "confirmed"); }
        if (previousFocus && previousFocus.isConnected) previousFocus.focus();
    });
    // Native POST navigation: observe after form-specific and validation handlers.
    window.addEventListener("submit", e => {
        if (e.defaultPrevented) return;
        const form = e.target;
        if (!(form instanceof HTMLFormElement)) return;
        const method = e.submitter?.getAttribute("formmethod") || form.method;
        if (method.toLowerCase() !== "post" || form.dataset.feedback === "off") return;
        if (form.target && form.target !== "_self") return;
        if (pending || dialog.open) { e.preventDefault(); return; }
        pending = true;
        activeForm = form;
        form.setAttribute("aria-busy","true");
        // Do not disable form controls: their values must still reach the server.
        show("processing");
    });
    // A restored browser history entry must not retain a processing lock.
    window.addEventListener("pageshow", e => {
        if (!e.persisted) return;
        pending = false;
        if (activeForm) activeForm.removeAttribute("aria-busy");
        activeForm = null;
        if (dialog.open) dialog.close();
    });
    async function post(url, options, onSuccess) {
        if (pending || dialog.open) return false;
        pending = true;
        show("processing");
        let success = false;
        try {
            const headers = new Headers(options.headers);
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (token && !headers.has("RequestVerificationToken")) headers.set("RequestVerificationToken", token);
            const response = await fetch(url, {...options, headers});
            if (!response.ok) throw new Error("HTTP " + response.status);
            const result = await response.json();
            if (result.success !== true) {
                await show("error", ar ? "لم تكتمل العملية. راجع البيانات والصلاحيات." : "The operation was rejected. Check the details and permissions.");
                return false;
            }
            success = true;
            await show("success", result.message);
        } catch (error) {
            console.error("Admin operation:", error);
            await show("error");
        } finally {
            pending = false;
        }
        if (success && onSuccess) onSuccess();
        return success;
    }
    async function confirmPost(url, options, question, onSuccess) {
        if (pending || dialog.open) return false;
        if (!await show("confirm", question)) return false;
        return post(url, options, onSuccess);
    }
    function remove(url) {
        return confirmPost(url, {method:"POST"},
            ar ? "هل أنت متأكد من حذف هذا السجل؟" : "Are you sure you want to delete this record?",
            () => location.reload());
    }
    window.AdminFeedback = { show, post, confirmPost, remove };
    window.showAlert = (heading, text, icon = "success") => show(icon, text, heading);
    document.addEventListener("DOMContentLoaded", () => {
        if (dialog.dataset.error) show("error", dialog.dataset.error);
        else if (dialog.dataset.invalid === "true") show("warning");
        else if (dialog.dataset.success) show("success", dialog.dataset.success);
    });
})();
