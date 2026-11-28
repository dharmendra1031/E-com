"""Real HTTPS/ASP.NET middleware and Razor smoke test on the disposable CI database."""
import http.cookiejar
import json
import os
import ssl
import time
import urllib.error
import urllib.parse
import urllib.request
from html.parser import HTMLParser

BASE = "https://127.0.0.1:5443"
PASSWORD = os.environ["MARKETPLACE_HTTP_TEST_PASSWORD"]
with open(os.environ["MARKETPLACE_HTTP_TEST_IDS"], encoding="utf-8") as file:
    ids = json.load(file)


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class Page(HTMLParser):
    def __init__(self, html):
        super().__init__()
        self.token = None
        self.links = set()
        self.direction = None
        self.feed(html)

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "input" and attrs.get("name") == "__RequestVerificationToken":
            self.token = attrs.get("value")
        if tag == "a" and "href" in attrs:
            self.links.add(urllib.parse.urlsplit(attrs["href"]).path.lower())
        if tag == "html":
            self.direction = attrs.get("dir")


def client():
    # The ephemeral self-signed certificate is trusted only in this loopback smoke test.
    return urllib.request.build_opener(
        urllib.request.HTTPSHandler(context=ssl._create_unverified_context()),
        urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()), NoRedirect())


def request(opener, path, expected, form=None, language="en"):
    data = urllib.parse.urlencode(form).encode() if form is not None else None
    req = urllib.request.Request(BASE + path, data=data, headers={"Accept-Language": language})
    try:
        response = opener.open(req, timeout=15)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        body = response.read().decode("utf-8", errors="replace")
        assert response.code == expected, f"{path}: expected {expected}, got {response.code}"
        return body, response.headers


def login(opener, email):
    html, _ = request(opener, "/vendor-portal/login", 200)
    token = Page(html).token
    assert token, "Login antiforgery input missing"
    request(opener, "/vendor-portal/login", 400, {"Email": email, "Password": PASSWORD})
    _, headers = request(opener, "/vendor-portal/login", 302, {
        "Email": email, "Password": PASSWORD, "__RequestVerificationToken": token})
    cookie = next((value for value in headers.get_all("Set-Cookie", [])
                   if value.startswith("__Secure-EcomVendor=")), "")
    assert cookie and "secure" in cookie.lower() and "httponly" in cookie.lower(), "Vendor cookie flags missing"


def main():
    anon = client()
    for attempt in range(60):
        try:
            request(anon, "/vendor-portal/login", 200)
            break
        except (urllib.error.URLError, ConnectionError):
            time.sleep(2)
    else:
        raise AssertionError("Vendor web application did not start")

    b = f"/vendor-portal/store/{ids['b']}"
    a = f"/vendor-portal/store/{ids['a']}"
    _, headers = request(anon, b, 302)
    assert "/vendor-portal/login" in headers.get("Location", "").lower()

    owner = client()
    login(owner, "owner@example.test")
    routes = ["", "/listings", "/inventory", "/orders", "/returns", "/finance", "/reports", "/profile"]
    html, _ = request(owner, b, 200)
    dashboard = Page(html)
    assert dashboard.direction == "ltr"
    assert all(b.lower() + route in dashboard.links for route in routes), "Owner sidebar missing routes"
    for route in routes:
        request(owner, b + route, 200)
        request(owner, a + route, 404)
    report, _ = request(owner, b + "/reports", 200)
    assert "190.000" in report, "Vendor report total differs from SQL fixture"
    request(owner, b + "/reports?from=2020-01-01&to=2021-01-01", 400)
    ar, _ = request(owner, b, 200, language="ar")
    assert Page(ar).direction == "rtl", "Arabic RTL markup missing"

    request(owner, "/vendor-portal/logout", 400, {})
    html, _ = request(owner, b, 200)
    token = Page(html).token
    assert token, "Logout antiforgery input missing"
    request(owner, "/vendor-portal/logout", 302, {"__RequestVerificationToken": token})
    request(owner, b, 302)

    staff = client()
    login(staff, "staff@example.test")
    html, _ = request(staff, b, 200)
    links = Page(html).links
    assert b + "/finance" not in links and b + "/reports" not in links, "Staff finance menu exposed"
    request(staff, b + "/orders", 200)
    request(staff, b + "/finance", 404)
    request(staff, b + "/reports", 404)
    request(staff, b + "/inventory", 404)
    print("Vendor HTTPS smoke passed: login, cookies, CSRF, eight rendered pages, cross-vendor denial, RTL markup, logout and staff restrictions.")


if __name__ == "__main__":
    main()
