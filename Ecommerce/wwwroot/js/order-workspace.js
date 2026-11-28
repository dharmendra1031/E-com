(() => {
    "use strict";
    const root=document.getElementById("orderWorkspace");
    if(!root)return;
    const ar=document.documentElement.dir==="rtl";
    const t=(en,arabic)=>ar?arabic:en;
    const $=id=>document.getElementById(id);
    const filters=$("orderFilters"), results=$("orderResults"), register=$("orderRegister");
    const detail=$("orderDetail"), content=$("orderDetailContent"), editor=$("orderStatusForm");
    const select=$("newOrderStatus"), save=$("saveOrderStatus"), password=$("orderPassword");
    const error=t("Unable to load. Please try again.","تعذر التحميل. حاول مرة أخرى.");
    let listRequest,detailRequest,statusRequest,orderId,blocked=false,operation,busy=false,lastButton;
    let currentUrl=new URL(location.href);
    async function get(url,signal,json=true){
        const response=await fetch(url,{signal,headers:{"Accept":json?"application/json":"text/html"}});
        if(!response.ok||response.redirected)throw Error(error);
        return json?response.json():response.text();
    }
    async function loadRegister(url) {
        listRequest?.abort();const request=new AbortController();listRequest=request;
        const target=new URL(url,location.origin);target.searchParams.delete("partial");
        const endpoint=new URL(target);endpoint.searchParams.set("partial","true");
        results.setAttribute("aria-busy","true");$("orderRegisterMessage").textContent=t("Loading orders…","جارٍ تحميل الطلبات…");
        try {
            const html=await get(endpoint,request.signal,false);
            if(request.signal.aborted)return;
            results.innerHTML=html;currentUrl=target;history.replaceState(null,"",target);
            $("orderRegisterMessage").textContent="";
        } catch(e){if(!request.signal.aborted)$("orderRegisterMessage").textContent=error;}
        finally{if(listRequest===request)results.removeAttribute("aria-busy");}
    }
    filters.addEventListener("submit",event=>{
        event.preventDefault();
        if(filters.startDate.value&&filters.endDate.value&&filters.startDate.value>filters.endDate.value){
            AdminFeedback.show("warning",t("End date must not precede start date.","يجب ألا يسبق تاريخ النهاية تاريخ البداية."));return;
        }
        const url=new URL(root.dataset.registerUrl,location.origin);
        for(const [key,value] of new FormData(filters))if(value)url.searchParams.set(key,value);
        loadRegister(url);
    });
    $("clearOrderFilters").addEventListener("click",event=>{
        event.preventDefault();
        for(const field of filters.querySelectorAll("select,input[type=date]"))field.value="";
        filters.requestSubmit();
    });
    results.addEventListener("click",event=>{
        const link=event.target.closest("[data-order-page]");
        if(link){if(event.ctrlKey||event.metaKey||event.shiftKey||event.altKey)return;event.preventDefault();loadRegister(link.href);return;}
        const button=event.target.closest("[data-order-id]");
        if(button){lastButton=button;openOrder(Number(button.dataset.orderId),button.dataset.paymentStatus==="3");}
    });
    const money=value=>value==null?"—":"KD "+Number(value).toFixed(3);
    function closeEditor(){
        statusRequest?.abort();if(!editor)return;editor.hidden=true;editor.reset();select.replaceChildren();operation=null;save.disabled=true;
    }
    async function openOrder(id,isBlocked){
        detailRequest?.abort();closeEditor();
        const request=new AbortController();detailRequest=request;orderId=id;blocked=isBlocked;
        register.hidden=true;detail.hidden=false;content.hidden=true;$("retryOrder").hidden=true;if($("printOrder"))$("printOrder").disabled=true;
        $("detailNumber").textContent="#"+id;
        $("orderDetailMessage").textContent=t("Loading order…","جارٍ تحميل الطلب…");
        $("orderDetailHeading").focus();
        try{
            const response=await get("/ManageOrders/GetOrderDetails/"+encodeURIComponent(id),request.signal);
            if(request.signal.aborted)return;
            if(response.success!==true||!response.data)throw Error(error);
            const data=response.data;
            for(const el of content.querySelectorAll("[data-detail]")){
                let val=data[el.dataset.detail];
                if(el.dataset.detail==="createdAt"&&val)val=new Date(val).toLocaleDateString(ar?"ar-KW":"en-GB");
                el.textContent=val==null||val===""?"—":String(val);
            }
            for(const el of content.querySelectorAll("[data-money]"))el.textContent=money(data[el.dataset.money]);
            content.querySelector("[data-percent]").textContent=data.vatpersent==null?"":"("+Number(data.vatpersent).toFixed(2)+"%)";
            const body=$("orderItemsBody");body.replaceChildren();
            for(const [i,item] of (data.items||[]).entries()){
                const row=document.createElement("tr");
                for(const value of [i+1,item.productName,item.quantity,money(item.price),money(item.quantity*item.price)]){
                    const cell=document.createElement("td");cell.textContent=value??"—";row.append(cell);
                }body.append(row);
            }
            if($("orderStatusActions"))$("orderStatusActions").hidden=blocked;content.hidden=false;if($("printOrder"))$("printOrder").disabled=false;$("orderDetailMessage").textContent="";
        }catch(e){if(!request.signal.aborted){$("orderDetailMessage").textContent=error;$("retryOrder").hidden=false;}}
    }
    $("retryOrder").addEventListener("click",()=>openOrder(orderId,blocked));
    $("backToOrders").addEventListener("click",()=>{
        if(busy)return;
        detailRequest?.abort();closeEditor();detail.hidden=true;register.hidden=false;
        if(lastButton?.isConnected)lastButton.focus();
    });
    $("printOrder")?.addEventListener("click",()=>window.open("/Report/OrderInvoicePrint?"+new URLSearchParams({OrderId:orderId,format:"PDF"}),"_blank","noopener"));
    if(!editor)return;
    $("orderStatusActions").addEventListener("click",async event=>{
        const button=event.target.closest("[data-operation]");
        if(!button||busy||blocked)return;
        closeEditor();operation=button.dataset.operation;
        const request=new AbortController();statusRequest=request;
        editor.hidden=false;$("orderStatusHeading").textContent=button.textContent;
        $("orderRemarkGroup").hidden=operation!=="order";
        $("orderStatusMessage").textContent=t("Loading available statuses…","جارٍ تحميل الحالات…");
        try{
            const rows=await get("/ManageOrders/"+(operation==="order"?"GetStatusList":"PaymentstatusList")+"?"+new URLSearchParams({orderId}),request.signal);
            if(request.signal.aborted)return;
            for(const row of rows)select.add(new Option(row.text,row.value,false,row.selected));
            save.disabled=select.options.length===0;$("orderStatusMessage").textContent="";
            select.focus();
        }catch(e){if(!request.signal.aborted)$("orderStatusMessage").textContent=error;}
    });
    $("cancelOrderStatus").addEventListener("click",()=>{if(!busy)closeEditor();});
    editor.addEventListener("submit",async event=>{
        event.preventDefault();if(busy||blocked||save.disabled||!operation)return;
        busy=true;save.disabled=true;$("backToOrders").disabled=true;$("cancelOrderStatus").disabled=true;
        const id=orderId,op=operation,status=Number(select.value),remark=$("orderRemark").value;
        $("orderStatusMessage").textContent=t("Verifying…","جارٍ التحقق…");
        try{
            const response=await fetch("/ManageOrders/VerifyPassword",{method:"POST",headers:{"Content-Type":"application/json","RequestVerificationToken":document.querySelector('input[name="__RequestVerificationToken"]').value},body:JSON.stringify({password:password.value,orderId:id,operation:op})});
            password.value="";
            if(!response.ok||response.redirected)throw Error(error);
            if((await response.json()).valid!==true){$("orderStatusMessage").textContent=t("Incorrect password or verification denied.","كلمة المرور غير صحيحة أو تم رفض التحقق.");return;}
            $("orderStatusMessage").textContent="";
            const success=await AdminFeedback.post("/ManageOrders/"+(op==="order"?"UpdateOrderStatus":"UpdatePaymentStatus"),op==="order"?{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({orderId:id,currentStatus:status,remark})}:{method:"POST",body:new URLSearchParams({orderId:id,PaymentStatus:status})});
            if(success){closeEditor();await Promise.all([loadRegister(currentUrl),openOrder(id,op==="payment"?status===3:blocked)]);}
            else $("orderStatusMessage").textContent=t("Check the current status before retrying.","تحقق من الحالة الحالية قبل إعادة المحاولة.");
        }catch(e){$("orderStatusMessage").textContent=error;}
        finally{password.value="";busy=false;save.disabled=select.options.length===0;$("backToOrders").disabled=false;$("cancelOrderStatus").disabled=false;}
    });
})();
