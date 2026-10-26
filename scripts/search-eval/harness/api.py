"""A signed-in session against a ShipLean instance. Every non-2xx answer is an ApiError."""
import requests


class ApiError(Exception):
    pass


class Api:
    def __init__(self, base_url, session=None):
        self.base = base_url.rstrip("/")
        self.http = session or requests.Session()
        # Writes must send JSON or this header (CrossSiteWriteGuard).
        self.http.headers["X-ShipLean-Client"] = "search-eval"

    def sign_in(self, email, password):
        self.send("POST", "/api/auth/login", json={"email": email, "password": password})

    def send(self, method, path, params=None, json=None):
        try:
            r = self.http.request(method, self.base + path, params=params, json=json, timeout=60)
        except requests.RequestException as e:
            raise ApiError(f"{method} {path}: {e}") from e
        if not r.ok:
            raise ApiError(f"{method} {path} answered {r.status_code}: {r.text[:300]}")
        return r.json() if r.content else None
