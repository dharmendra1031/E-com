(() => {
    "use strict";
    const root=document.getElementById("masterRegister");
    if(!root)return;
    const form=document.getElementById("masterFilters"),results=document.getElementById("masterResults");
    const message=document.getElementById("masterMessage"),name=form.elements.namedItem("name"),suggestions=document.getElementById("masterSuggestions");
    const ar=document.documentElement.dir==="rtl",t=(en,arabic)=>ar?arabic:en;
    const failure=t("Unable to load records. Please try again.","تعذر تحميل السجلات. حاول مرة أخرى.");
    let currentUrl=new URL(location.href),request,suggestRequest,timer,busy=false;
    async function load(url){
        request?.abort();const active=new AbortController();request=active;
        const target=new URL(url,location.origin);target.searchParams.delete("partial");
        const endpoint=new URL(target);endpoint.searchParams.set("partial","true");
        results.setAttribute("aria-busy","true");message.textContent=t("Loading…","جارٍ التحميل…");
        try{
            const response=await fetch(endpoint,{signal:active.signal,headers:{Accept:"text/html"}});
            if(!response.ok||response.redirected)throw Error(failure);
            const html=await response.text();if(active.signal.aborted)return;
            results.innerHTML=html;currentUrl=target;history.replaceState(null,"",target);message.textContent="";
        }catch(e){if(!active.signal.aborted)message.textContent=failure;}
        finally{if(request===active)results.removeAttribute("aria-busy");}
    }
    form.addEventListener("submit",e=>{
        e.preventDefault();if(busy)return;
        const url=new URL(root.dataset.registerUrl,location.origin);
        for(const [key,value] of new FormData(form))if(String(value).trim())url.searchParams.set(key,String(value).trim());
        load(url);
    });
    document.getElementById("clearMasterFilters").addEventListener("click",e=>{
        e.preventDefault();if(busy)return;
        for(const field of form.querySelectorAll("select,input[type=search]"))field.value="";
        suggestions.replaceChildren();form.requestSubmit();
    });
    name.addEventListener("input",()=>{
        clearTimeout(timer);suggestRequest?.abort();suggestions.replaceChildren();
        const term=name.value.trim();if(term.length<2)return;
        timer=setTimeout(async()=>{
            const active=new AbortController();suggestRequest=active;
            try{
                const url=new URL(root.dataset.suggestUrl,location.origin);url.searchParams.set("term",term);
                const response=await fetch(url,{signal:active.signal});
                if(!response.ok||response.redirected) return;
                const rows=await response.json();if(active.signal.aborted||name.value.trim()!==term)return;
                for(const row of rows){const option=document.createElement("option");option.value=row;suggestions.append(option);}
            }catch(e){/* Search remains available when suggestions fail. */}
        },250);
    });
    results.addEventListener("click",async e=>{
        const page=e.target.closest("[data-register-page]");
        if(page){if(e.ctrlKey||e.metaKey||e.shiftKey||e.altKey)return;e.preventDefault();if(!busy)load(page.href);return;}
        const remove=e.target.closest("[data-delete-url]");
        if(remove){
            if(busy||results.getAttribute("aria-busy")==="true")return;
            busy=true;
            try{
                if(await AdminFeedback.confirmPost(remove.dataset.deleteUrl,{method:"POST"},t("Delete this record?","هل تريد حذف هذا السجل؟")))await load(currentUrl);
            }finally{busy=false;}
        }
        const add=e.target.closest("[data-add-stock]");
        if(add&&!busy&&results.getAttribute("aria-busy")!=="true")openStock(add);
    });
    const stock=document.getElementById("stockEditor");
    const stockForm=document.getElementById("stockForm");
    function openStock(button){
        if(!stock)return;
        stockForm.reset();stockForm.elements.namedItem("productId").value=button.dataset.productid;
        document.getElementById("stockProduct").textContent=button.dataset.productname;
        document.getElementById("stockAvailable").textContent=button.dataset.remaining;
        stock.hidden=false;stockForm.elements.namedItem("newStock").focus();
    }
    if(stockForm){
        document.getElementById("cancelStock").addEventListener("click",()=>{if(!busy){stock.hidden=true;stockForm.reset();}});
        stockForm.addEventListener("submit",async e=>{
            e.preventDefault();if(busy)return;busy=true;
            try{
                const success=await AdminFeedback.post(stockForm.dataset.postUrl,{method:"POST",body:new URLSearchParams(new FormData(stockForm))});
                if(success){stock.hidden=true;stockForm.reset();await load(currentUrl);}
            }finally{busy=false;}
        });
    }
})();
