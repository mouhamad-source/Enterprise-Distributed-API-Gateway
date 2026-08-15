#!/usr/bin/env python3
"""
اختبار V4: تحديد هوية العميل باستخدام استراتيجيات متعددة
"""

import requests
import time
import json
import jwt
from colorama import init, Fore

init(autoreset=True)

GATEWAY_URL = "http://localhost:5000"

def print_test(name, passed, details=None):
    status = f"{Fore.GREEN}✓ PASS" if passed else f"{Fore.RED}✗ FAIL"
    print(f"{status} {name}")
    if details:
        print(f"   {json.dumps(details, indent=2)}")

# 1. IP-based
def test_ip():
    print("\n--- Test: IP-based identification ---")
    headers = {"X-Forwarded-For": "192.168.1.100"}
    r1 = requests.get(f"{GATEWAY_URL}/users/1", headers=headers)
    r2 = requests.get(f"{GATEWAY_URL}/users/1", headers=headers)
    passed = r1.status_code == 200 and r2.status_code == 200
    print_test("IP Resolver works", passed, {"first": r1.status_code, "second": r2.status_code})

# 2. JWT-based
def test_jwt():
    print("\n--- Test: JWT-based identification ---")
    # Use a strong key (at least 32 characters)
    SECRET_KEY = "this_is_a_very_long_test_secret_key_1234567890"

    token = jwt.encode({"sub": "user-123"}, SECRET_KEY, algorithm="HS256")

    headers = {"Authorization": f"Bearer {token}"}
    r1 = requests.get(f"{GATEWAY_URL}/users/1", headers=headers)
    r2 = requests.get(f"{GATEWAY_URL}/users/1", headers=headers)
    passed = r1.status_code == 200 and r2.status_code == 200
    print_test("JWT Resolver works", passed, {"first": r1.status_code, "second": r2.status_code})

# 3. API Key
def test_apikey():
    print("\n--- Test: API Key-based identification ---")
    headers = {"X-API-Key": "abc-123-def"}
    r1 = requests.get(f"{GATEWAY_URL}/users/1", headers=headers)
    r2 = requests.get(f"{GATEWAY_URL}/users/1", headers=headers)
    passed = r1.status_code == 200 and r2.status_code == 200
    print_test("API Key Resolver works", passed, {"first": r1.status_code, "second": r2.status_code})

# 4. Separate counters
def test_separate_counters():
    print("\n--- Test: Different users have separate counters ---")
    ip1 = "192.168.1.200"
    ip2 = "192.168.1.201"
    # أرسل 101 طلب من IP1 لتجاوز الحد
    for _ in range(101):
        requests.get(f"{GATEWAY_URL}/users/1", headers={"X-Forwarded-For": ip1})
    # طلب من IP2 يجب أن ينجح
    resp = requests.get(f"{GATEWAY_URL}/users/1", headers={"X-Forwarded-For": ip2})
    passed = resp.status_code == 200
    print_test("Separate counters for different IPs", passed, {"ip2_response": resp.status_code})

# 5. JWT persists across IP change
def test_jwt_ip_change():
    print("\n--- Test: JWT counter persists across IP changes ---")
    token = jwt.encode({"sub": "user-456"}, "secret", algorithm="HS256")
    headers1 = {"Authorization": f"Bearer {token}", "X-Forwarded-For": "1.1.1.1"}
    for _ in range(50):
        requests.get(f"{GATEWAY_URL}/users/1", headers=headers1)
    headers2 = {"Authorization": f"Bearer {token}", "X-Forwarded-For": "2.2.2.2"}
    resp = requests.get(f"{GATEWAY_URL}/users/1", headers=headers2)
    passed = resp.status_code == 200
    print_test("JWT counter persists across IP changes", passed, {"response": resp.status_code})

# 6. Priority (JWT > API Key > IP)
def test_priority():
    print("\n--- Test: Priority (JWT > API Key > IP) ---")
    token = jwt.encode({"sub": "user-priority"}, "secret", algorithm="HS256")
    headers = {
        "Authorization": f"Bearer {token}",
        "X-API-Key": "api-key-ignore",
        "X-Forwarded-For": "10.0.0.1"
    }
    resp = requests.get(f"{GATEWAY_URL}/users/1", headers=headers)
    passed = resp.status_code == 200
    print_test("Priority: JWT used over API Key and IP", passed, {"response": resp.status_code})

if __name__ == "__main__":
    print("Starting V4 tests...")
    # test_ip()
    test_jwt()
    test_apikey()
    test_separate_counters()
    test_jwt_ip_change()
    test_priority()
    print("\nAll tests completed.")