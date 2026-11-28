(() => {
    "use strict";
    const form=document.getElementById("themeForm");
    if(!form)return;
    const input=document.getElementById("selectedColor"),picker=document.getElementById("colorPicker"),preview=document.getElementById("themePreview");
    const ar=document.documentElement.dir==="rtl",valid=value=>/^#[0-9a-f]{6}$/i.test(value);
    let busy=false;
    const match=document.getElementById("matchLogoColor");
    if(match){
        const logo=document.getElementById("themeCompanyLogo");
        const palette=document.getElementById("logoPalette");
        const message=document.getElementById("logoColorMessage");
        match.addEventListener("click",async ()=>{
            if(busy)return;
            match.disabled=true;
            palette.hidden=true;palette.replaceChildren();
            message.textContent=ar?"جارٍ تحليل الشعار…":"Analyzing logo…";
            try{
                await logo.decode();
                if(!logo.naturalWidth||!logo.naturalHeight)throw Error();
                const canvas=document.createElement("canvas");
                const scale=Math.min(1,128/Math.max(logo.naturalWidth,logo.naturalHeight));
                canvas.width=Math.max(1,Math.round(logo.naturalWidth*scale));
                canvas.height=Math.max(1,Math.round(logo.naturalHeight*scale));
                const context=canvas.getContext("2d");
                context.drawImage(logo,0,0,canvas.width,canvas.height);
                const colors=extractLogoColors(context.getImageData(0,0,canvas.width,canvas.height).data);
                if(!colors.length){
                    message.textContent=ar?"لم يتم العثور على ألوان مناسبة.":"No usable logo colors found.";
                    return;
                }
                for(const color of colors){
                    const button=document.createElement("button");
                    button.type="button";button.className="theme-swatch";
                    button.dataset.color=color;button.style.background=color;
                    button.title=color;button.setAttribute("aria-label",color);
                    button.setAttribute("aria-pressed",String(input.value.toUpperCase()===color));
                    palette.append(button);
                }
                palette.hidden=false;
                message.textContent=ar?"اختر لوناً للمعاينة ثم احفظه.":"Choose a matching color to preview, then save it.";
            }catch{
                message.textContent=ar?"تعذر تحليل الشعار. تحقق من الصورة المحفوظة.":"Unable to analyze the logo. Check the saved image.";
            }finally{match.disabled=false;}
        });
    }

    function extractLogoColors(pixels){
        const groups=new Map();
        for(let i=0;i+3<pixels.length;i+=4){
            const r=pixels[i],g=pixels[i+1],b=pixels[i+2];
            if(pixels[i+3]<128)continue;
            const max=Math.max(r,g,b),min=Math.min(r,g,b);
            if(min>=240)continue; // Ignore white background.
            const key=[r>>5,g>>5,b>>5].join(",");
            let group=groups.get(key);
            if(!group){group={count:0,r:0,g:0,b:0};groups.set(key,group);}
            group.count++;group.r+=r;group.g+=g;group.b+=b;
        }
        const ranked=Array.from(groups.values()).map(g=>{
            const rgb=[g.r,g.g,g.b].map(v=>Math.round(v/g.count));
            return {rgb,count:g.count,chroma:Math.max(...rgb)-Math.min(...rgb)};
        });
        const colorful=ranked.filter(g=>g.chroma>=35);
        const candidates=colorful.length?colorful:ranked;
        candidates.sort((a,b)=>b.count-a.count);
        const selected=[];
        for(const candidate of candidates){
            if(candidate.count<Math.max(1,candidates[0].count*0.025))continue;
            if(selected.some(rgb=>rgb.reduce((sum,v,i)=>sum+(v-candidate.rgb[i])**2,0)<55**2))continue;
            selected.push(candidate.rgb);
            if(selected.length===5)break;
        }
        return selected.map(rgb=>"#"+rgb.map(v=>v.toString(16).padStart(2,"0")).join("").toUpperCase());
    }
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
