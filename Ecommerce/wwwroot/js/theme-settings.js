(() => {
    "use strict";
    const form=document.getElementById("themeForm");
    if(!form)return;
    const input=document.getElementById("selectedColor"),picker=document.getElementById("colorPicker"),preview=document.getElementById("themePreview");
    const ar=document.documentElement.dir==="rtl",valid=value=>/^#[0-9a-f]{6}$/i.test(value);
    let busy=false;
    function select(value){
        if(!valid(value))return;
        input.value=value.toUpperCase();picker.value=value;
        preview.style.background=value;
        const linear=c=>{const n=c/255;return n<=0.04045?n/12.92:((n+0.055)/1.055)**2.4;};
        const l=0.2126*linear(parseInt(value.slice(1,3),16))+0.7152*linear(parseInt(value.slice(3,5),16))+0.0722*linear(parseInt(value.slice(5,7),16));
        preview.style.color=(l+0.05)/0.05>=1.05/(l+0.05)?"#000":"#fff";
        for(const swatch of form.querySelectorAll("[data-color]"))swatch.setAttribute("aria-pressed",String(swatch.dataset.color.toUpperCase()===input.value));
    }
    form.addEventListener("click",e=>{const swatch=e.target.closest("[data-color]");if(swatch&&!busy)select(swatch.dataset.color);});
    picker.addEventListener("input",()=>select(picker.value));
    input.addEventListener("input",()=>{if(valid(input.value))select(input.value);});
    form.addEventListener("submit",async e=>{
        e.preventDefault();if(busy||!valid(input.value))return;
        busy=true;const save=form.querySelector("button[type=submit]");save.disabled=true;
        const message=document.getElementById("themeMessage");message.textContent="";
        try{
            const ok=await AdminFeedback.post(form.action,{method:"POST",body:new URLSearchParams(new FormData(form))});
            if(!ok)return;
            const response=await fetch(form.dataset.readUrl,{cache:"no-store"});
            if(!response.ok||response.redirected)throw Error();
            const data=await response.json();
            if(!data.success||!data.theme||!valid(data.color))throw Error();
            const theme=data.theme;
            for(const [key,value] of Object.entries({primary:theme.primary,foreground:theme.foreground,hover:theme.hover,"hover-foreground":theme.hoverForeground,link:theme.link,tint:theme.tint,rgb:theme.rgb})){
                if(key==="rgb"?/^\d{1,3},\d{1,3},\d{1,3}$/.test(value):valid(value))document.documentElement.style.setProperty("--theme-"+key,value);
            }
            select(data.color);
        }catch{message.textContent=ar?"تعذر تحديث المعاينة. أعد فتح الصفحة للتحقق من اللون المحفوظ.":"Unable to refresh the theme. Reopen this page to verify the saved color.";}
        finally{busy=false;save.disabled=false;}
    });
})();
