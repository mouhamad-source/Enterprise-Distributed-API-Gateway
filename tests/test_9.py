import requests
import time
import json
from datetime import datetime
import os

GATEWAY_URL = "http://localhost:5000"

def clear_screen():
    os.system('cls' if os.name == 'nt' else 'clear')

def get_metrics():
    try:
        resp = requests.get(f"{GATEWAY_URL}/metrics", timeout=5)
        if resp.status_code == 200:
            return resp.text
    except:
        pass
    return None

def parse_metrics(text):
    """استخراج بعض المقاييس الأساسية من نص Prometheus"""
    metrics = {}
    for line in text.split('\n'):
        if line.startswith('#') or line.strip() == '':
            continue
        parts = line.split(' ')
        if len(parts) >= 2:
            name = parts[0]
            # تجاهل الأسماء التي تحتوي على { (labels)
            if '{' not in name:
                try:
                    value = float(parts[1])
                    metrics[name] = value
                except:
                    pass
    return metrics

def show_dashboard():
    while True:
        clear_screen()
        print("=" * 60)
        print(f"🚪 GATEWAY DASHBOARD - {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
        print("=" * 60)

        metrics_text = get_metrics()
        if metrics_text:
            metrics = parse_metrics(metrics_text)

            # عرض المقاييس المهمة
            print("\n📊 Metrics:")
            print(f"  Requests Total:        {metrics.get('gateway_requests_total', 0):.0f}")
            print(f"  Rate Limit Rejected:   {metrics.get('gateway_rate_limit_rejected_total', 0):.0f}")
            print(f"  Auth Failures:         {metrics.get('gateway_authentication_failures_total', 0):.0f}")
            print(f"  Circuit Breaker Open:  {metrics.get('gateway_circuit_breaker_open_total', 0):.0f}")
            print(f"  Retry Attempts:        {metrics.get('gateway_retry_attempts_total', 0):.0f}")
            print(f"  Service Unavailable:   {metrics.get('gateway_service_unavailable_total', 0):.0f}")

            # عرض معلومات إضافية من /health
            try:
                health = requests.get(f"{GATEWAY_URL}/health", timeout=2)
                if health.status_code == 200:
                    print(f"\n✅ Health Status: {health.json().get('status', 'unknown')}")
                else:
                    print(f"\n❌ Health Status: {health.status_code}")
            except:
                print("\n❌ Health Status: Unreachable")

        else:
            print("❌ Cannot fetch metrics. Is Gateway running?")

        print("\n" + "-" * 60)
        print("⏹ Press Ctrl+C to exit")
        print("⏳ Refreshing in 5 seconds...")
        time.sleep(5)

if __name__ == "__main__":
    try:
        show_dashboard()
    except KeyboardInterrupt:
        print("\n👋 Dashboard stopped.")