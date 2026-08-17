import requests
import time
from collections import Counter

GATEWAY_URL = "http://localhost:5000"
INSTANCES = ["5001", "5002", "5003"]

def test_round_robin():
    responses = []
    for i in range(30):
        try:
            resp = requests.get(f"{GATEWAY_URL}/users/1", timeout=10)
            responses.append(resp.status_code)
        except requests.exceptions.RequestException as e:
            responses.append(("EXCEPTION", str(e)))
        time.sleep(0.1)

    status_counter = Counter()
    for r in responses:
        if isinstance(r, tuple):
            status_counter[r[0]] += 1
        else:
            status_counter[r] += 1

    print("Status code distribution:")
    for status, count in status_counter.items():
        print(f"  {status}: {count}")

    success = sum(1 for r in responses if r == 200)
    print(f"Successful requests: {success}/{len(responses)}")

def test_instance_failure():
    print("Stop instance 5002 now (e.g., kill the process). Press Enter to continue...")
    input()

    for i in range(10):
        try:
            resp = requests.get(f"{GATEWAY_URL}/users/1", timeout=10)
            print(f"Request {i+1}: {resp.status_code}")
        except requests.exceptions.RequestException as e:
            print(f"Request {i+1}: EXCEPTION - {e}")
        time.sleep(0.2)

if __name__ == "__main__":
    test_round_robin()
    test_instance_failure()