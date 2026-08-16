import requests
import jwt
import time
from colorama import init, Fore

init(autoreset=True)

GATEWAY_URL = "http://localhost:5000"

# Matching JWT settings from appsettings.json
SECRET = "c2VjcmV0LWtleS1mb3ItZ2F0ZXdheS1hdXRoZW50aWNhdGlvbi1tdXN0LWJlLWxvbmc="
ISSUER = "http://localhost:5000"
AUDIENCE = "http://localhost:5000"

def generate_token(user_id, plan="Free", role="User", expired=False, wrong_issuer=False, wrong_aud=False, wrong_sig=False):
    payload = {
        "sub": user_id,
        "plan": plan,
        "role": role,
        "iss": "wrong-issuer" if wrong_issuer else ISSUER,
        "aud": "wrong-aud" if wrong_aud else AUDIENCE,
        "exp": int(time.time()) - 10 if expired else int(time.time()) + 3600
    }
    secret = "wrong-secret-key-that-is-also-at-least-32-chars!" if wrong_sig else SECRET
    return jwt.encode(payload, secret, algorithm="HS256")

def test_auth():
    print("\n--- Authentication Tests ---")

    # 1. Valid token
    token = generate_token("user-1", "Free")
    resp = requests.get(f"{GATEWAY_URL}/users/1", headers={"Authorization": f"Bearer {token}"})
    print(f"Valid token: {resp.status_code} (expected 200)")

    # 2. Expired token
    token_exp = generate_token("user-2", "Free", expired=True)
    resp = requests.get(f"{GATEWAY_URL}/users/1", headers={"Authorization": f"Bearer {token_exp}"})
    print(f"Expired token: {resp.status_code} (expected 401)")

    # 3. Invalid signature
    token_bad_sig = generate_token("user-3", "Free", wrong_sig=True)
    resp = requests.get(f"{GATEWAY_URL}/users/1", headers={"Authorization": f"Bearer {token_bad_sig}"})
    print(f"Invalid signature: {resp.status_code} (expected 401)")

    # 4. Wrong issuer
    token_bad_iss = generate_token("user-4", "Free", wrong_issuer=True)
    resp = requests.get(f"{GATEWAY_URL}/users/1", headers={"Authorization": f"Bearer {token_bad_iss}"})
    print(f"Wrong issuer: {resp.status_code} (expected 401)")

    # 5. Wrong audience
    token_bad_aud = generate_token("user-5", "Free", wrong_aud=True)
    resp = requests.get(f"{GATEWAY_URL}/users/1", headers={"Authorization": f"Bearer {token_bad_aud}"})
    print(f"Wrong audience: {resp.status_code} (expected 401)")

def test_rate_limit():
    print("\n--- Rate Limit Tests ---")

    def send_burst(token, plan_name, expected_allowed):
        headers = {"Authorization": f"Bearer {token}"}
        success = 0
        for i in range(200):
            try:
                resp = requests.get(f"{GATEWAY_URL}/users/1", headers=headers)
                if resp.status_code == 200:
                    success += 1
                elif resp.status_code == 429:
                    break
            except requests.exceptions.RequestException:
                break
        print(f"{plan_name}: {success} succeeded (expected <= {expected_allowed})")
        return success

    # Free user (limit 100)
    token_free = generate_token("free-user", "Free")
    send_burst(token_free, "Free", 100)

    # Premium user (limit 5000)
    token_prem = generate_token("premium-user", "Premium")
    send_burst(token_prem, "Premium", 5000)

    # Admin (unlimited)
    token_admin = generate_token("admin-user", "Admin", "Admin")
    send_burst(token_admin, "Admin", 200)  # Testing 200 requests burst

    # No token (should fall back to IP or API Key)
    try:
        resp = requests.get(f"{GATEWAY_URL}/users/1")
        print(f"No token: {resp.status_code} (expected 200, 401, or 429)")
    except requests.exceptions.RequestException as e:
        print(f"No token request failed: {e}")

if __name__ == "__main__":
    test_auth()
    test_rate_limit()