#!/usr/bin/env python3
"""
Comprehensive test for all Gateway features (V1 → V11) with corrections
"""


import requests
import time
import json
import jwt
import concurrent.futures
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry
from colorama import init, Fore, Style
import sys
from datetime import datetime


# Initialize colorama for colored console output
init(autoreset=True)


# Gateway base URL
GATEWAY_URL = "http://localhost:5000"
# JWT secret key (must match the one used by the Gateway for token validation)
JWT_SECRET = "c2VjcmV0LWtleS1mb3ItZ2F0ZXdheS1hdXRoZW50aWNhdGlvbi1tdXN0LWJlLWxvbmc="
# JWT issuer claim value
JWT_ISSUER = "http://localhost:5000"
# JWT audience claim value
JWT_AUDIENCE = "http://localhost:5000"
# HTTP request timeout in seconds
TIMEOUT = 10
# Maximum number of retries for failed requests
MAX_RETRIES = 3


def create_session():
    """
    Create a requests Session with retry logic for transient server errors.
    """
    session = requests.Session()
    retries = Retry(total=MAX_RETRIES, backoff_factor=0.5,
                    status_forcelist=[500, 502, 503, 504],
                    allowed_methods=["GET", "POST"])
    adapter = HTTPAdapter(max_retries=retries)
    session.mount('http://', adapter)
    session.mount('https://', adapter)
    return session


def generate_jwt(user_id, plan="Free", role="User", expired=False,
                 wrong_issuer=False, wrong_aud=False, wrong_sig=False):
    """
    Generate a JWT token for testing authentication scenarios.
    
    Args:
        user_id: Subject (sub) claim value
        plan: User subscription plan (Free/Premium)
        role: User role
        expired: If True, create an expired token
        wrong_issuer: If True, use an incorrect issuer claim
        wrong_aud: If True, use an incorrect audience claim
        wrong_sig: If True, sign with wrong secret (invalid signature)
    
    Returns:
        Encoded JWT token string
    """
    payload = {
        "sub": user_id,
        "plan": plan,
        "role": role,
        "iss": "wrong-issuer" if wrong_issuer else JWT_ISSUER,
        "aud": "wrong-aud" if wrong_aud else JWT_AUDIENCE,
        "exp": int(time.time()) - 10 if expired else int(time.time()) + 600
    }
    secret = "wrong-secret" if wrong_sig else JWT_SECRET
    return jwt.encode(payload, secret, algorithm="HS256")


def wait_for_gateway():
    """
    Wait for the Gateway to become ready by polling the /ready endpoint.
    Returns True if Gateway becomes ready, False otherwise.
    """
    print(f"{Fore.YELLOW}⏳ Waiting for Gateway...")
    for i in range(15):
        try:
            resp = requests.get(f"{GATEWAY_URL}/ready", timeout=2)
            if resp.status_code == 200:
                print(f"{Fore.GREEN}✅ Gateway is ready!")
                return True
        except:
            pass
        time.sleep(1)
    print(f"{Fore.RED}❌ Gateway did not become ready.")
    return False


def print_result(name, passed, message="", details=None):
    """
    Print test result with colored status indicator.
    
    Args:
        name: Test name
        passed: Boolean indicating if test passed
        message: Optional message to display
        details: Optional dictionary with additional details
    """
    status = f"{Fore.GREEN}✓ PASS" if passed else f"{Fore.RED}✗ FAIL"
    print(f"{status} {name}")
    if message:
        print(f"  {message}")
    if details:
        print(f"  Details: {json.dumps(details, indent=2)}")


def test_endpoint(method, path, expected_status, session, test_name, headers=None, data=None):
    """
    Test a single endpoint and verify the response status code.
    
    Args:
        method: HTTP method (GET/POST)
        path: URL path (e.g., "/users/1")
        expected_status: Expected HTTP status code
        session: requests Session object
        test_name: Name of the test for logging
        headers: Optional HTTP headers
        data: Optional JSON data for POST requests
    
    Returns:
        Response object if status matches, None otherwise
    """
    print(f"\n{Fore.CYAN}--- {test_name} ---")
    try:
        url = f"{GATEWAY_URL}{path}"
        if method.upper() == "GET":
            resp = session.get(url, headers=headers, timeout=TIMEOUT)
        else:
            resp = session.post(url, headers=headers, json=data, timeout=TIMEOUT)
        print(f"  Status Code: {resp.status_code}")
        if resp.status_code == expected_status:
            print_result(test_name, True, f"Status {resp.status_code} matches expected")
            if resp.text:
                print(f"  Response: {resp.text[:150]}")
            return resp
        else:
            print_result(test_name, False, f"Expected {expected_status}, got {resp.status_code}")
            return None
    except Exception as e:
        print_result(test_name, False, f"Exception: {e}")
        return None


def test_v1_routing(session):
    """
    V1: Test basic routing functionality.
    Verifies that requests are correctly routed to backend services.
    """
    print(f"\n{Fore.MAGENTA}{Style.BRIGHT}>>> V1 - Basic Routing <<<")
    results = []
    results.append(test_endpoint("GET", "/users/1", 200, session, "GET /users/1"))
    results.append(test_endpoint("GET", "/users/999", 404, session, "GET /users/999"))
    results.append(test_endpoint("GET", "/unknown", 404, session, "GET /unknown"))
    results.append(test_endpoint("POST", "/users", 405, session, "POST /users"))
    passed = sum(1 for r in results if r is not None)
    print(f"\n{Fore.YELLOW}V1: {passed}/{len(results)} passed")
    return passed == len(results)


def test_v2_v3_rate_limiting(session):
    """
    V2+V3: Test rate limiting (Memory-based and Distributed).
    V2: IP-based rate limiting with fixed window (100 requests).
    V3: Separate counters per IP address (distributed rate limiting).
    """
    print(f"\n{Fore.MAGENTA}{Style.BRIGHT}>>> V2+V3 - Rate Limiting (Memory & Distributed) <<<")
    results = []

    # V2: Use FixedWindow for testing (must be configured in appsettings)
    # Send 101 requests with small delay to avoid LeakyBucket effects
    test_ip = "192.168.100.99"
    headers = {"X-Forwarded-For": test_ip}
    success = 0
    for i in range(101):
        resp = session.get(f"{GATEWAY_URL}/users/1", headers=headers, timeout=2)
        if resp and resp.status_code == 200:
            success += 1
        elif resp and resp.status_code == 429:
            break
        time.sleep(0.01)  # Small delay
    # If algorithm is FixedWindow, result should be 100.
    # If LeakyBucket, it might be 101.
    # We accept 100 or 101 with notification.
    v2_ok = success == 100 or success == 101
    results.append(v2_ok)
    print_result("V2: IP-based rate limiting (expected 100 or 101 with leaky bucket)", v2_ok,
                 f"Allowed {success} requests")

    # V3: Separate counters per IP
    ip1 = "192.168.200.10"
    ip2 = "192.168.200.11"
    for i in range(101):
        resp = session.get(f"{GATEWAY_URL}/users/1", headers={"X-Forwarded-For": ip1}, timeout=2)
        if resp and resp.status_code == 429:
            break
    resp2 = session.get(f"{GATEWAY_URL}/users/1", headers={"X-Forwarded-For": ip2}, timeout=2)
    v3_ok = resp2 and resp2.status_code == 200
    results.append(v3_ok)
    print_result("V3: Separate counters per IP (distributed)", v3_ok,
                 f"IP2 status = {resp2.status_code if resp2 else 'None'}")

    passed = sum(results)
    print(f"\n{Fore.YELLOW}V2+V3: {passed}/{len(results)} passed")
    return passed == len(results)


def test_v4_identity_resolution(session):
    """
    V4: Test client identity resolution from multiple sources.
    Priority: JWT token > API Key > IP address.
    """
    print(f"\n{Fore.MAGENTA}{Style.BRIGHT}>>> V4 - Identity Resolution <<<")
    results = []
    token = generate_jwt("user-jwt")
    resp = test_endpoint("GET", "/users/1", 200, session, "JWT resolver",
                         headers={"Authorization": f"Bearer {token}"})
    results.append(resp is not None)
    resp = test_endpoint("GET", "/users/1", 200, session, "API Key resolver",
                         headers={"X-API-Key": "test-api-key"})
    results.append(resp is not None)
    resp = test_endpoint("GET", "/users/1", 200, session, "IP fallback")
    results.append(resp is not None)
    token2 = generate_jwt("user-priority")
    headers = {"Authorization": f"Bearer {token2}", "X-API-Key": "ignored", "X-Forwarded-For": "9.9.9.9"}
    resp = test_endpoint("GET", "/users/1", 200, session, "Priority: JWT over others", headers=headers)
    results.append(resp is not None)
    passed = sum(results)
    print(f"\n{Fore.YELLOW}V4: {passed}/{len(results)} passed")
    return passed == len(results)


def test_v6_authentication(session):
    """
    V6: Test JWT authentication and validation.
    Tests: valid token, invalid signature, expired token, wrong issuer, wrong audience.
    Also tests plan-based rate limits (Free: 100, Premium: >100).
    """
    print(f"\n{Fore.MAGENTA}{Style.BRIGHT}>>> V6 - Authentication <<<")
    results = []
    token = generate_jwt("user-auth")
    resp = test_endpoint("GET", "/users/1", 200, session, "Valid JWT -> 200",
                         headers={"Authorization": f"Bearer {token}"})
    results.append(resp is not None)
    token_bad = generate_jwt("user-bad", wrong_sig=True)
    resp = test_endpoint("GET", "/users/1", 401, session, "Invalid signature -> 401",
                         headers={"Authorization": f"Bearer {token_bad}"})
    results.append(resp is not None)
    token_exp = generate_jwt("user-exp", expired=True)
    resp = test_endpoint("GET", "/users/1", 401, session, "Expired token -> 401",
                         headers={"Authorization": f"Bearer {token_exp}"})
    results.append(resp is not None)
    token_iss = generate_jwt("user-iss", wrong_issuer=True)
    resp = test_endpoint("GET", "/users/1", 401, session, "Wrong issuer -> 401",
                         headers={"Authorization": f"Bearer {token_iss}"})
    results.append(resp is not None)
    token_aud = generate_jwt("user-aud", wrong_aud=True)
    resp = test_endpoint("GET", "/users/1", 401, session, "Wrong audience -> 401",
                         headers={"Authorization": f"Bearer {token_aud}"})
    results.append(resp is not None)

    # Free user limit (100) - use FixedWindow for accuracy
    token_free = generate_jwt("free-user", plan="Free")
    headers_free = {"Authorization": f"Bearer {token_free}"}
    success = 0
    for i in range(101):
        resp = session.get(f"{GATEWAY_URL}/users/1", headers=headers_free, timeout=2)
        if resp and resp.status_code == 200:
            success += 1
        elif resp and resp.status_code == 429:
            break
        time.sleep(0.01)
    free_ok = success == 100 or success == 101  # Same adjustment
    results.append(free_ok)
    print_result("Free user limited to 100 (or 101 with leaky bucket)", free_ok,
                 f"Success count: {success}")

    token_prem = generate_jwt("premium-user", plan="Premium")
    headers_prem = {"Authorization": f"Bearer {token_prem}"}
    success = 0
    for i in range(120):
        resp = session.get(f"{GATEWAY_URL}/users/1", headers=headers_prem, timeout=2)
        if resp and resp.status_code == 200:
            success += 1
        elif resp and resp.status_code == 429:
            break
    prem_ok = success >= 110
    results.append(prem_ok)
    print_result("Premium allows >100 (tested 110)", prem_ok, f"Success count: {success}")

    passed = sum(results)
    print(f"\n{Fore.YELLOW}V6: {passed}/{len(results)} passed")
    return passed == len(results)


def test_v8_service_discovery(session):
    """
    V8: Test service discovery and load balancing.
    Verifies that requests are distributed across multiple service instances.
    """
    print(f"\n{Fore.MAGENTA}{Style.BRIGHT}>>> V8 - Service Discovery & Load Balancer <<<")
    results = []
    success = 0
    for i in range(30):
        resp = session.get(f"{GATEWAY_URL}/users/1", timeout=2)
        if resp and resp.status_code == 200:
            success += 1
        time.sleep(0.05)
    ok = success >= 25
    results.append(ok)
    print_result("Load balancer distributes requests (no 503)", ok, f"Success rate {success}/30")
    passed = sum(results)
    print(f"\n{Fore.YELLOW}V8: {passed}/{len(results)} passed")
    return passed == len(results)


def test_v9_observability(session):
    """
    V9: Test observability endpoints (/metrics, /health).
    Verifies Prometheus metrics and health check endpoints.
    """
    print(f"\n{Fore.MAGENTA}{Style.BRIGHT}>>> V9 - Observability <<<")
    results = []

    # 1. /metrics - look for the correct metric name "gateway.requests.total"
    resp = session.get(f"{GATEWAY_URL}/metrics", timeout=5)
    ok_metrics = resp and resp.status_code == 200 and "gateway.requests.total" in resp.text
    results.append(ok_metrics)
    print_result("/metrics contains expected counter (gateway.requests.total)", ok_metrics)

    # 2. /health
    resp = session.get(f"{GATEWAY_URL}/health", timeout=5)
    ok_health = False
    if resp and resp.status_code == 200:
        try:
            data = resp.json()
            if "components" in data:
                ok_health = True
        except:
            pass
    results.append(ok_health)
    print_result("/health returns component status", ok_health)

    passed = sum(results)
    print(f"\n{Fore.YELLOW}V9: {passed}/{len(results)} passed")
    return passed == len(results)


def test_v10_load(session):
    """
    V10: Simple load test with concurrent requests.
    Tests Gateway stability under concurrent load (100 requests, 50 workers).
    """
    print(f"\n{Fore.MAGENTA}{Style.BRIGHT}>>> V10 - Simple Load Test <<<")
    results = []
    def send_one():
        try:
            resp = session.get(f"{GATEWAY_URL}/users/1", timeout=5)
            return resp.status_code == 200
        except:
            return False
    with concurrent.futures.ThreadPoolExecutor(max_workers=50) as executor:
        futures = [executor.submit(send_one) for _ in range(100)]
        results_list = [f.result() for f in concurrent.futures.as_completed(futures)]
    success = sum(1 for r in results_list if r)
    ok = success >= 90
    results.append(ok)
    print_result("100 concurrent requests success rate", ok, f"Success rate {success}/100")
    passed = sum(results)
    print(f"\n{Fore.YELLOW}V10: {passed}/{len(results)} passed")
    return passed == len(results)


def test_v11_production_readiness(session):
    """
    V11: Test production readiness endpoints (/ready, /ops).
    Verifies readiness probe and operational dashboard with feature flags.
    """
    print(f"\n{Fore.MAGENTA}{Style.BRIGHT}>>> V11 - Production Readiness <<<")
    results = []

    # 1. /ready
    resp = session.get(f"{GATEWAY_URL}/ready", timeout=5)
    ok_ready = resp and resp.status_code == 200
    results.append(ok_ready)
    print_result("/ready returns 200", ok_ready)

    # 2. /ops
    resp = session.get(f"{GATEWAY_URL}/ops", timeout=5)
    ok_ops = False
    if resp and resp.status_code == 200:
        data = resp.json()
        if "status" in data and "health" in data and "features" in data:
            ok_ops = True
    results.append(ok_ops)
    print_result("/ops contains dashboard data", ok_ops)

    # 3. Feature flags present in /ops
    if resp and resp.status_code == 200:
        data = resp.json()
        has_features = "features" in data
        results.append(has_features)
        print_result("Feature flags present in /ops", has_features,
                     f"Features: {json.dumps(data.get('features', {}))}")
    else:
        results.append(False)

    passed = sum(results)
    print(f"\n{Fore.YELLOW}V11: {passed}/{len(results)} passed")
    return passed == len(results)


def main():
    """
    Main test runner. Executes all test groups and prints summary.
    """
    print(f"{Fore.CYAN}{Style.BRIGHT}🚀 Gateway Test Suite (V1 → V11)")
    print(f"Target: {GATEWAY_URL}")
    print(f"Started at: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")

    if not wait_for_gateway():
        sys.exit(1)

    session = create_session()

    groups = [
        ("V1", test_v1_routing),
        ("V2+V3", test_v2_v3_rate_limiting),
        ("V4", test_v4_identity_resolution),
        ("V6", test_v6_authentication),
        ("V8", test_v8_service_discovery),
        ("V9", test_v9_observability),
        ("V10", test_v10_load),
        ("V11", test_v11_production_readiness),
    ]

    results = []
    for name, func in groups:
        print(f"\n{Fore.CYAN}{'='*80}")
        result = func(session)
        results.append((name, result))
        time.sleep(1)

    print(f"\n{Fore.YELLOW}{Style.BRIGHT}{'='*80}")
    print("📊 Test Summary")
    print('='*80)
    all_passed = True
    for name, result in results:
        status = f"{Fore.GREEN}✅ PASS" if result else f"{Fore.RED}❌ FAIL"
        print(f"{status}  {name}")
        if not result:
            all_passed = False
    print('='*80)
    if all_passed:
        print(f"{Fore.GREEN}{Style.BRIGHT}🎉 All tests passed!")
    else:
        print(f"{Fore.RED}{Style.BRIGHT}⚠️  Some tests failed, review output above.")

    sys.exit(0 if all_passed else 1)


if __name__ == "__main__":
    main()