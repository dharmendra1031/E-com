(() => {
    "use strict";
    const $=id=>document.getElementById(id), root=$("masterRegister"), detail=$("customerDetail");
    if(!detail)return;
    const register=$("customerRegister"), content=$("customerContent"), message=$("customerMessage");
    const ar=document.documentElement.dir==="rtl",t=(en,arabic)=>ar?arabic:en;
    const error=t("Unable to load. Please try again.","تعذر التحميل. حاول مرة أخرى.");
    let id,active,page=1,pages=1,itemPage=1,itemPages=1,orderId,request,itemRequest,busy=false,lastButton;
    const status=$("customerStatus"), back=$("backCustomers");
    const value=v=>v==null||v===""?"—":String(v);
    const date=v=>v?new Date(v).toLocaleDateString(ar?"ar-KW":"en-GB"):"—";
    const money=v=>v==null?"—":"KD "+Number(v).toFixed(3);
    async function get(endpoint,params,signal){
        const url=new URL(endpoint,location.origin);url.search=new URLSearchParams(params);
        const response=await fetch(url,{signal,cache:"no-store"});
        if(!response.ok||response.redirected)throw Error(error);
        return response.json();
    }
    function cells(body,values){
        const row=document.createElement("tr");
        for(const v of values){const cell=document.createElement("td");cell.textContent=value(v);row.append(cell);}
        body.append(row);return row;
    }
    function closeItems(){
        itemRequest?.abort();$("customerItems").hidden=true;$("customerItemRows").replaceChildren();
    }
    async function load(targetPage=1){
        request?.abort();closeItems();const current=new AbortController();request=current;
        register.hidden=true;detail.hidden=false;content.hidden=true;
        $("retryCustomer").hidden=true;message.textContent=t("Loading customer…","جارٍ تحميل العميل…");
        try{
            const data=await get(detail.dataset.detailsUrl,{id,page:targetPage},current.signal);
            if(current.signal.aborted)return;
            const c=data.customer;active=c.status===true;
            for(const el of content.querySelectorAll("[data-customer]")){
                const key=el.dataset.customer;
                el.textContent=key==="status"?(active?t("Active","نشط"):t("Blocked","محظور")):key==="registrationDate"?date(c[key]):value(c[key]);
            }
            const image=$("customerImage");image.hidden=!c.customerImage;
            if(c.customerImage)image.src=c.customerImage;else image.removeAttribute("src");
            status.textContent=active?t("Block customer","حظر العميل"):t("Unblock customer","إلغاء حظر العميل");
            status.className="btn "+(active?"btn-danger":"btn-success");
            const addresses=$("customerAddresses");addresses.replaceChildren();
            if(!data.addresses.length)addresses.textContent=t("No saved addresses","لا توجد عناوين محفوظة");
            for(const a of data.addresses){
                const box=document.createElement("div");box.className="customer-address";
                const title=document.createElement("strong");title.textContent=value(a.title)+(a.isDefault?" · "+t("Default","افتراضي"):"");box.append(title);
                for(const [label,v] of [[t("Area","المنطقة"),a.addressArea],[t("Block","القطعة"),a.addressBlock],[t("Street","الشارع"),a.addressStreet],[t("House","المنزل"),a.addressHouseNo],[t("Floor","الطابق"),a.addressFloor],[t("Landmark","علامة مميزة"),a.addressLandmark]]){
                    if(!v)continue;const p=document.createElement("p");p.textContent=label+": "+v;box.append(p);
                }addresses.append(box);
            }
            const rows=$("customerOrders");rows.replaceChildren();
            for(const o of data.orders){
                const row=cells(rows,["#"+o.orderId,date(o.date),o.status,o.paymentStatus,money(o.totalAmount)]);
                const cell=document.createElement("td"),button=document.createElement("button");
                button.type="button";button.className="btn btn-default";button.dataset.customerOrder=o.orderId;button.textContent=t("Products","المنتجات");cell.append(button);row.append(cell);
            }
            if(!data.orders.length){const row=cells(rows,[t("No store orders","لا توجد طلبات")]);row.firstChild.colSpan=6;}
            page=data.page;pages=data.pages;$("customerOrderCount").textContent=data.total+" "+t("orders","طلب");$("customerPage").textContent=page+" / "+pages;
            $("customerPrev").disabled=page<=1;$("customerNext").disabled=page>=pages;
            content.hidden=false;message.textContent="";
        }catch(e){if(!current.signal.aborted){message.textContent=error;$("retryCustomer").hidden=false;}}
    }
    $("masterResults").addEventListener("click",e=>{
        const button=e.target.closest("[data-customer-id]");
        if(!button||busy||$("masterResults").getAttribute("aria-busy")==="true")return;
        lastButton=button;id=Number(button.dataset.customerId);page=1;load();$("customerHeading").focus();
    });
    back.addEventListener("click",()=>{
        if(busy)return;request?.abort();closeItems();detail.hidden=true;register.hidden=false;
        if(lastButton?.isConnected)lastButton.focus();
    });
    $("retryCustomer").addEventListener("click",()=>load(page));
    $("customerPrev").addEventListener("click",()=>{if(!busy&&page>1)load(page-1);});
    $("customerNext").addEventListener("click",()=>{if(!busy&&page<pages)load(page+1);});
    $("customerPassword").addEventListener("click",()=>{if(!busy)showChangePwdForm(id);});
    status.addEventListener("click",async()=>{
        if(busy)return;busy=true;status.disabled=true;back.disabled=true;
        try{
            const next=!active;
            const success=await AdminFeedback.confirmPost(detail.dataset.statusUrl,{method:"POST",body:new URLSearchParams({id,active:next})},
                next?t("Unblock this customer?","إلغاء حظر هذا العميل؟"):t("Block this customer?","حظر هذا العميل؟"));
            if(success){root.dispatchEvent(new Event("register:refresh"));await load(page);}
        }finally{busy=false;status.disabled=false;back.disabled=false;}
    });
    async function loadItems(targetPage){
        itemRequest?.abort();const current=new AbortController();itemRequest=current;
        $("customerItems").hidden=false;$("customerOrderId").textContent="#"+orderId;
        $("customerItemRows").replaceChildren();$("itemsPrev").disabled=true;$("itemsNext").disabled=true;$("itemsPage").textContent="";
        $("customerItemsMessage").textContent=t("Loading products…","جارٍ تحميل المنتجات…");
        try{
            const data=await get(detail.dataset.itemsUrl,{id,orderId,page:targetPage},current.signal);
            if(current.signal.aborted)return;
            for(const item of data.items)cells($("customerItemRows"),[item.productName,item.quantity,money(item.price),money(item.totalPrice)]);
            if(!data.items.length){const row=cells($("customerItemRows"),[t("No products found","لا توجد منتجات")]);row.firstChild.colSpan=4;}
            itemPage=data.page;itemPages=data.pages;$("itemsPage").textContent=itemPage+" / "+itemPages;$("itemsPrev").disabled=itemPage<=1;$("itemsNext").disabled=itemPage>=itemPages;
            $("customerItemsMessage").textContent="";
        }catch(e){if(!current.signal.aborted)$("customerItemsMessage").textContent=error;}
    }
    $("customerOrders").addEventListener("click",e=>{const button=e.target.closest("[data-customer-order]");if(button&&!busy){orderId=Number(button.dataset.customerOrder);loadItems(1);}});
    $("closeCustomerItems").addEventListener("click",closeItems);
    $("itemsPrev").addEventListener("click",()=>{if(itemPage>1)loadItems(itemPage-1);});
    $("itemsNext").addEventListener("click",()=>{if(itemPage<itemPages)loadItems(itemPage+1);});
})();
