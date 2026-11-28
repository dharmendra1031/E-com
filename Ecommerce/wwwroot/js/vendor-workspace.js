(() => {
    "use strict";
    const ar=document.documentElement.dir==="rtl";
    const t=(en,arabic)=>ar?arabic:en;
    const root=document.querySelector("[data-seller-workspace]");
    if(root){
        const results=root.querySelector("[data-seller-results]");
        const message=root.querySelector(".seller-message");
        let request;
        async function load(url){
            request?.abort();
            const active=new AbortController();request=active;
            const target=new URL(url,location.origin);target.searchParams.delete("partial");
            const endpoint=new URL(target);endpoint.searchParams.set("partial","true");
            results.setAttribute("aria-busy","true");message.textContent=t("Loading…","جارٍ التحميل…");
            try{
                const response=await fetch(endpoint,{signal:active.signal,cache:"no-store",headers:{Accept:"text/html"}});
                if(!response.ok||response.redirected)throw Error("Request failed");
                const html=await response.text();if(active.signal.aborted)return;
                results.innerHTML=html;history.replaceState(null,"",target);message.textContent="";
            }catch(e){if(!active.signal.aborted)message.textContent=t("Unable to load records. Try again.","تعذر تحميل السجلات. حاول مرة أخرى.");}
            finally{if(request===active)results.removeAttribute("aria-busy");}
        }
        results.addEventListener("submit",e=>{
            const form=e.target;
            if(!form.matches("[data-seller-filter]"))return;
            e.preventDefault();const url=new URL(form.action);
            for(const [key,value] of new FormData(form))if(String(value).trim())url.searchParams.set(key,String(value).trim());
            load(url);
        });
        results.addEventListener("click",e=>{
            const link=e.target.closest("[data-seller-page]");
            if(!link||e.ctrlKey||e.metaKey||e.shiftKey||e.altKey)return;
            e.preventDefault();load(link.href);
        });
    }
    document.addEventListener("submit",e=>{
        const form=e.target;
        if(!form.matches("[data-seller-save]")||e.defaultPrevented)return;
        if(form.dataset.saving){e.preventDefault();return;}
        form.dataset.saving="true";form.setAttribute("aria-busy","true");
        for(const button of form.querySelectorAll("button[type=submit]"))button.disabled=true;
    });
    window.addEventListener("pageshow",e=>{
        if(!e.persisted)return;
        for(const form of document.querySelectorAll("[data-seller-save][data-saving]")){
            delete form.dataset.saving;form.removeAttribute("aria-busy");
            for(const button of form.querySelectorAll("button[type=submit]"))button.disabled=false;
        }
    });
})();
