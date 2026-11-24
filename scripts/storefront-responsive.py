"""Read-only storefront responsive smoke test.
Install once: python -m pip install playwright
             python -m playwright install chromium firefox webkit
Run against your running development instance:
    python scripts/storefront-responsive.py --base-url http://localhost:5229
    python scripts/storefront-responsive.py --base-url http://localhost:5229 --browser webkit
Optional reference audit (public English/Arabic sites):
    python scripts/storefront-responsive.py --reference --browser chromium
Screenshots and JSON results are written outside the repository by default.
Never submits orders or changes application data.
"""
import argparse
import json
import tempfile
from pathlib import Path
from urllib.parse import urlencode
from playwright.sync_api import sync_playwright

SIZES = [(320,568),(360,800),(390,844),(430,932),(844,390),
         (768,1024),(820,1180),(1024,768),(1280,720),(1366,768),
         (1440,900),(1920,1080),(2560,1440),(3840,2160)]
ROUTES = [("home","/WebHome/Index"),("catalog","/WebCategory/Index"),
          ("brands","/BrandWiseProducts/Index"),("deals","/Wekenddealwiseproducts/Index"),
          ("cart","/WebCart/Cart"),("login","/UserAccount/Login"),
          ("register","/UserAccount/Register"),("contact","/WebContact/Contact")]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--base-url", default="http://localhost:5229")
parser.add_argument("--browser", choices=["chromium","firefox","webkit"], default="chromium")
parser.add_argument("--reference", action="store_true")
parser.add_argument("--product-id", type=int, help="Existing public product ID for product-detail coverage")
parser.add_argument("--checkout", action="store_true", help="Check checkout page without submitting an order; requires a valid cart")
parser.add_argument("--output", type=Path, default=Path(tempfile.gettempdir())/"ecom-responsive")
args = parser.parse_args()
if args.product_id is not None:
    ROUTES.append(("product", "/WebSingleProduct/SingleProduct/"+str(args.product_id)))
if args.checkout:
    ROUTES.append(("checkout", "/WebCheckout/Index"))
args.output.mkdir(parents=True, exist_ok=True)
report = []

with sync_playwright() as pw:
    browser = getattr(pw, args.browser).launch()
    for width, height in SIZES:
        for language in ("en","ar"):
            context = browser.new_context(viewport={"width":width,"height":height},
                device_scale_factor=1, has_touch=width<=1024)
            page = context.new_page()
            base = (("https://ar.wibi.com.kw" if language=="ar" else "https://wibi.com.kw")
                    if args.reference else args.base_url.rstrip("/"))
            routes = [("home","/"),("catalog","/collections/networking-products")] if args.reference else ROUTES
            if not args.reference:
                page.goto(base+"/Home/SetLanguage?"+urlencode({"culture":language,"returnUrl":"/WebHome/Index"}))
            for name, route in routes:
                row = {"browser":args.browser,"language":language,"width":width,"height":height,"page":name,"issues":[]}
                try:
                    response = page.goto(base+route, wait_until="domcontentloaded", timeout=30000)
                    if not response or response.status>=400:
                        raise RuntimeError("HTTP "+str(response.status if response else "no response"))
                    if not args.reference:
                        if name=="catalog":
                            page.locator("#ProductContainer").wait_for(state="visible")
                            page.wait_for_function("document.querySelector('#ProductContainer').textContent.trim().length > 0 && !document.querySelector('#ProductContainer').hasAttribute('aria-busy')", timeout=10000)
                    page.evaluate("document.fonts.ready")
                    metrics = page.evaluate("""() => {
                        const root=document.documentElement;
                        const header=document.querySelector('#siteHeader');
                        const body=document.querySelector('#pageContent');
                        const search=document.querySelector('#searchInput');
                        const rect=el=>el?.getBoundingClientRect();
                        const controls = [...document.querySelectorAll("#siteHeader .store-header-action, #siteHeader .header-actions > .btn, .store-product-grid .add-to-cart-btn")].filter(el=>rect(el).width && rect(el).height && (rect(el).width<43.5 || rect(el).height<43.5)).map(el=>el.className);
                        const badImages = [...document.querySelectorAll(".store-product-grid .cart-image-container")].filter(el=>rect(el).height && Math.abs(rect(el).width/rect(el).height-4/3)>.05).length;
                        const clippedCards = [...document.querySelectorAll(".store-product-grid .cart-item-card, .store-product-grid .cart-item-footer, .store-product-grid .product-price")].filter(el=>el.scrollWidth>el.clientWidth+2).length;
                        const overflow=[...document.querySelectorAll('body *')].filter(el=>{
                          if (!el.getClientRects().length || getComputedStyle(el).visibility==='hidden') return false;
                          if (el.closest('.overflow-auto,.category-scroll,.category-list,.storefront-brands,.product-thumbnails,.table-responsive,.carousel,.offcanvas,.dropdown-menu')) return false;
                          const r=rect(el);
                          return r.width>0 && r.height>0 && (r.right>innerWidth+2 || r.left< -2);
                        }).slice(0,12).map(el=>el.tagName+'.'+String(el.className).slice(0,100));
                        return {lang:root.lang,dir:root.dir||getComputedStyle(root).direction,
                          viewport:root.clientWidth,scrollWidth:root.scrollWidth, controls,badImages,clippedCards,
                          headerHeight:rect(header)?.height||0,
                          headerOverlaps:header && body && getComputedStyle(header).position==='fixed'
                            ? rect(body).top+parseFloat(getComputedStyle(body).paddingTop)<rect(header).bottom-2:false,
                          searchVisible:search ? !!(rect(search).width&&rect(search).height):null,
                          overflow};
                    }""")
                    row["metrics"]=metrics
                    if metrics["scrollWidth"]>metrics["viewport"]+2: row["issues"].append("horizontal document overflow")
                    if metrics["overflow"]: row["issues"].append("elements outside viewport")
                    if not args.reference:
                        if metrics["dir"]!=("rtl" if language=="ar" else "ltr"): row["issues"].append("wrong direction")
                        if metrics["lang"]!=language: row["issues"].append("wrong language")
                        if metrics["headerOverlaps"]: row["issues"].append("header overlaps page")
                        if not metrics["searchVisible"]: row["issues"].append("search hidden")
                        if metrics["controls"]: row["issues"].append("undersized touch targets")
                        if metrics["badImages"]: row["issues"].append("product image aspect ratio")
                        if metrics["clippedCards"]: row["issues"].append("product card clipping")
                    page.screenshot(path=str(args.output/f"{args.browser}-{language}-{width}x{height}-{name}.png"), full_page=False, animations="disabled")
                except Exception as error:
                    row["issues"].append(str(error))
                report.append(row)
                print(("FAIL" if row["issues"] else "PASS"),language,width,height,name, row["issues"])
            context.close()
    browser.close()
(args.output/f"{args.browser}-{'reference' if args.reference else 'storefront'}.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
print("Checked",len(report),"viewports/pages; failures:",sum(bool(x["issues"]) for x in report))
raise SystemExit(1 if any(x["issues"] for x in report) else 0)
