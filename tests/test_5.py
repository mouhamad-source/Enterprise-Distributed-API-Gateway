import time
import requests

GATEWAY_URL = "http://localhost:5000"
HEADERS = {"X-Forwarded-For": "192.168.1.222"}

def send_batch(count):
    return sum(
        1 for _ in range(count)
        if requests.get(f"{GATEWAY_URL}/users/1", headers=HEADERS).status_code == 200
    )

def test_rate_limit():
    print("--- Testing Rate Limiting ---")
    
    # Batch 1: Immediate requests
    b1 = send_batch(100)
    print(f"Batch 1 (10 reqs): {b1} succeeded")
    
    time.sleep(1)
    
    # Batch 2: After 1 sec
    b2 = send_batch(100)
    print(f"Batch 2 (10 reqs after 1s): {b2} succeeded")
    
    # Results
    total = b1 + b2
    if total <= 50:
        print(f"✅ Success: Rate limit enforced ({total}/20 allowed)")
    else:
        print(f"⚠️ Warning: Burst allowed ({total}/20 allowed)")

if __name__ == "__main__":
    print("Ensure 'Algorithm' in appsettings.json is set before testing.")
    test_rate_limit()