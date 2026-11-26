(() => {
    "use strict";
    const form = document.getElementById("NavigationKey");
    if (!form) return;
    const ar = document.documentElement.lang === "ar";
    const t = (en, arabic) => ar ? arabic : en;
    const category = form.querySelector("#CategoryDropdown");
    const subcategory = form.querySelector("#SubCategoryDropdown");
    let request;
    let loading = false;
    let failed = false;
    category.addEventListener("change", async () => {
        request?.abort();
        const current = new AbortController();
        request = current;
        loading = false;
        failed = false;
        subcategory.replaceChildren(new Option(t("Select subcategory", "اختر الفئة الفرعية"), ""));
        if (!category.value) { subcategory.disabled = false; return; }
        loading = true;
        subcategory.disabled = true;
        subcategory.options[0].text = t("Loading…", "جارٍ التحميل…");
        try {
            const response = await fetch(form.dataset.subcategoriesUrl + "?" + new URLSearchParams({categoryId:category.value}), {signal:current.signal});
            if (!response.ok || response.redirected) throw new Error();
            const rows = await response.json();
            if (current.signal.aborted) return;
            subcategory.options[0].text = t("Select subcategory", "اختر الفئة الفرعية");
            for (const row of rows) subcategory.add(new Option(row.scName, row.subCategoryId));
        } catch(error) {
            if (current.signal.aborted) return;
            failed = true;
            subcategory.options[0].text = t("Unable to load — reselect category", "تعذر التحميل — أعد اختيار الفئة");
        } finally {
            if(request === current) { loading = false; subcategory.disabled = false; }
        }
    });
    form.addEventListener("submit", event => {
        const cost = Number(form.querySelector("#CoastPrice").value);
        const sale = Number(form.querySelector("#Price").value);
        if (loading || failed || !Number.isFinite(cost) || !Number.isFinite(sale) || sale < cost) {
            event.preventDefault();
            AdminFeedback.show("warning", loading || failed
                ? t("Please load the subcategories before saving.", "يرجى تحميل الفئات الفرعية قبل الحفظ.")
                : t("Sale price must be greater than or equal to cost price.", "يجب ألا يقل سعر البيع عن التكلفة."));
        }
    });

    const specs = form.querySelector("#specContainer");
    const fields = [
        ["SpeciNameAr","Arabic Name","الاسم بالعربية","col-md-2","rtl"],
        ["SpecivalueAr","Arabic Value","القيمة بالعربية","col-md-4","rtl"],
        ["SpeciNameEn","English Name","الاسم بالإنجليزية","col-md-2","ltr"],
        ["SpecivalueEn","English Value","القيمة بالإنجليزية","col-md-3","ltr"]
    ];
    function renumber() {
        specs.querySelectorAll(".specRow").forEach((row,index) => {
            row.querySelectorAll("input").forEach(input => {
                input.name = input.name.replace(/Specifications\[\d+\]/, "Specifications[" + index + "]");
            });
        });
    }
    form.querySelector("#addSpecRow").addEventListener("click", () => {
        const index = specs.querySelectorAll(".specRow").length;
        const row = document.createElement("div");
        row.className = "row specRow mb-2";
        for (const [name,en,arabic,cls,dir] of fields) {
            const col = document.createElement("div");
            col.className = cls;
            const input = document.createElement("input");
            input.className = "form-control";
            input.name = "Specifications[" + index + "]." + name;
            input.placeholder = t(en,arabic);
            input.setAttribute("aria-label",input.placeholder);
            input.dir = dir;
            col.append(input);row.append(col);
        }
        const col=document.createElement("div");
        col.className="col-md-1";
        const button=document.createElement("button");
        button.type="button";button.className="btn btn-danger removeSpecRow";button.textContent="×";
        button.setAttribute("aria-label",t("Remove specification","حذف مواصفة"));
        col.append(button);row.append(col);specs.append(row);row.querySelector("input").focus();
    });
    specs.addEventListener("click",event => {
        const button=event.target.closest(".removeSpecRow");
        if(!button)return;
        button.closest(".specRow").remove();renumber();
    });

    const preview = form.querySelector("#imagePreview");
    const images = form.querySelector("#ProductImages");
    let urls = [];
    images.addEventListener("change", () => {
        urls.forEach(url => URL.revokeObjectURL(url));
        urls=[];preview.replaceChildren();
        for(const file of images.files) {
            if (!file.type.startsWith("image/")) continue;
            const image=document.createElement("img");
            const url=URL.createObjectURL(file);urls.push(url);
            image.src=url;image.alt=file.name;preview.append(image);
        }
    });
    form.addEventListener("click",event => {
        const button=event.target.closest("[data-delete-image]");
        if(!button)return;
        const tile=button.closest("[data-product-image]");
        const gallery=tile.parentElement;
        AdminFeedback.confirmPost(form.dataset.deleteImageUrl,{method:"POST",body:new URLSearchParams({imageId:button.dataset.deleteImage})},
            t("Delete this image?","هل تريد حذف هذه الصورة؟"),()=>{
                tile.remove();
                if(!gallery.querySelector("[data-product-image]")){
                    const empty=document.createElement("span");empty.className="text-muted";
                    empty.textContent=t("No images uploaded","لا توجد صور");gallery.append(empty);
                }
            });
    });
})();
