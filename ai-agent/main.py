

import os
import json
import smtplib
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from groq import Groq
from dotenv import load_dotenv

# ─────────────────────────────────────────────
# Load secrets from .env file
# ─────────────────────────────────────────────
load_dotenv()

GROQ_API_KEY    = os.getenv("GROQ_API_KEY")
GROQ_MODEL      = os.getenv("GROQ_MODEL", "openai/gpt-oss-120b")
GMAIL_SENDER    = os.getenv("GMAIL_SENDER")
GMAIL_APP_PASS  = os.getenv("GMAIL_APP_PASS")
RECIPIENT_EMAIL = os.getenv("RECIPIENT_EMAIL")

INPUT_FILE  = "input/client_requirements.txt"
OUTPUT_FILE = "output/analysis_output.json"

# Validate all required secrets are present
missing = [k for k, v in {
    "GROQ_API_KEY": GROQ_API_KEY,
    "GMAIL_SENDER": GMAIL_SENDER,
    "GMAIL_APP_PASS": GMAIL_APP_PASS,
    "RECIPIENT_EMAIL": RECIPIENT_EMAIL,
}.items() if not v]

if missing:
    raise EnvironmentError(f"Missing required environment variables in .env: {', '.join(missing)}")
# ─────────────────────────────────────────────


def read_requirements(filepath: str) -> str:
    """Step 1: Read client requirement text file."""
    print(f"[1] Reading requirements from: {filepath}")
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()
    print(f"    ✓ Read {len(content)} characters")
    return content


def analyze_with_groq(requirement_text: str) -> dict:
    """Step 2: Send to Groq LLM and get structured analysis."""
    print("\n[2] Sending to Groq LLM for analysis...")

    client = Groq(api_key=GROQ_API_KEY)

    prompt = f"""
You are a senior Business Analyst. Analyze the following client requirement email and 
return a structured JSON response with EXACTLY these 5 keys:

1. "functional_requirements"   - List of specific features the system must do (strings)
2. "non_functional_requirements" - List of quality attributes (performance, security, scalability etc.)
3. "risks"                     - List of potential risks identified
4. "assumptions"               - List of assumptions made while reading the requirements
5. "questions_to_client"       - List of clarifying questions to ask the client

Return ONLY valid JSON. No markdown, no explanation, no extra text.

CLIENT REQUIREMENT EMAIL:
\"\"\"
{requirement_text}
\"\"\"
"""

    response = client.chat.completions.create(
        model=GROQ_MODEL,
        messages=[
            {
                "role": "system",
                "content": "You are a senior Business Analyst. Always respond with valid JSON only."
            },
            {
                "role": "user",
                "content": prompt
            }
        ],
        temperature=0.3,
        max_tokens=2000,
        response_format={"type": "json_object"}
    )

    raw_text = response.choices[0].message.content.strip()
    print(f"    ✓ Groq responded ({len(raw_text)} characters)")

    # Parse JSON response
    try:
        analysis = json.loads(raw_text)
    except json.JSONDecodeError:
        # Try to extract JSON from response if there's extra text
        import re
        json_match = re.search(r'\{.*\}', raw_text, re.DOTALL)
        if json_match:
            analysis = json.loads(json_match.group())
        else:
            raise ValueError(f"Could not parse JSON from Groq response:\n{raw_text}")

    # Save raw output
    os.makedirs("output", exist_ok=True)
    with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
        json.dump(analysis, f, indent=2, ensure_ascii=False)
    print(f"    ✓ Analysis saved to: {OUTPUT_FILE}")

    return analysis


def format_email_body(analysis: dict, original_text: str) -> str:
    """Step 3: Format the analysis as a professional HTML email."""
    print("\n[3] Formatting email body...")

    def list_to_html(items: list) -> str:
        return "".join(f"<li>{item}</li>" for item in items)

    html = f"""
<!DOCTYPE html>
<html>
<head>
<style>
  body {{ font-family: Arial, sans-serif; color: #333; max-width: 800px; margin: auto; }}
  h1 {{ color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 8px; }}
  h2 {{ color: #2980b9; margin-top: 24px; }}
  ul {{ line-height: 1.8; }}
  li {{ margin-bottom: 4px; }}
  .section {{ background: #f8f9fa; border-left: 4px solid #3498db; padding: 12px 20px; margin: 16px 0; border-radius: 4px; }}
  .footer {{ margin-top: 32px; font-size: 12px; color: #888; border-top: 1px solid #ddd; padding-top: 12px; }}
</style>
</head>
<body>

<h1>📋 Requirements Analysis Report</h1>
<p>Dear Client,</p>
<p>Thank you for sharing your project requirements. Below is our structured analysis 
based on your requirement email. Please review and confirm, or provide clarifications 
where needed.</p>

<div class="section">
<h2>✅ Functional Requirements</h2>
<ul>{list_to_html(analysis.get('functional_requirements', []))}</ul>
</div>

<div class="section">
<h2>⚙️ Non-Functional Requirements</h2>
<ul>{list_to_html(analysis.get('non_functional_requirements', []))}</ul>
</div>

<div class="section">
<h2>⚠️ Risks</h2>
<ul>{list_to_html(analysis.get('risks', []))}</ul>
</div>

<div class="section">
<h2>📌 Assumptions</h2>
<ul>{list_to_html(analysis.get('assumptions', []))}</ul>
</div>

<div class="section">
<h2>❓ Questions to Client</h2>
<ul>{list_to_html(analysis.get('questions_to_client', []))}</ul>
</div>

<p>We look forward to your response and are excited to work on this project with you.</p>

<p>Best Regards,<br>
<strong>Business Analysis Team</strong><br>
requirements-analysis@company.com</p>

<div class="footer">
  This email was generated automatically using AI-powered requirements analysis.<br>
  Groq LLM Model: {GROQ_MODEL}
</div>

</body>
</html>
"""
    print("    ✓ Email formatted")
    return html


def send_email(html_body: str, subject: str = "Requirements Analysis Report") -> bool:
    """Step 4: Send formatted email via Gmail SMTP."""
    print(f"\n[4] Sending email to: {RECIPIENT_EMAIL}")

    msg = MIMEMultipart("alternative")
    msg["Subject"] = subject
    msg["From"]    = GMAIL_SENDER
    msg["To"]      = RECIPIENT_EMAIL

    msg.attach(MIMEText(html_body, "html"))

    try:
        with smtplib.SMTP_SSL("smtp.gmail.com", 465) as server:
            server.login(GMAIL_SENDER, GMAIL_APP_PASS)
            server.sendmail(GMAIL_SENDER, RECIPIENT_EMAIL, msg.as_string())
        print(f"    ✓ Email sent successfully to {RECIPIENT_EMAIL}")
        return True
    except smtplib.SMTPAuthenticationError:
        print("    ✗ Gmail authentication failed. Check GMAIL_SENDER and GMAIL_APP_PASS in .env")
        print("      → Enable 2FA on Gmail → Create App Password at myaccount.google.com/apppasswords")
        return False
    except Exception as e:
        print(f"    ✗ Email send failed: {e}")
        return False


def save_email_html(html_body: str):
    """Save email as HTML file for review."""
    path = "output/formatted_email.html"
    with open(path, "w", encoding="utf-8") as f:
        f.write(html_body)
    print(f"    ✓ Email HTML saved to: {path}")


def print_summary(analysis: dict):
    """Print analysis summary to console."""
    print("\n" + "="*60)
    print("           REQUIREMENTS ANALYSIS SUMMARY")
    print("="*60)

    sections = {
        "✅ Functional Requirements": "functional_requirements",
        "⚙️  Non-Functional Requirements": "non_functional_requirements",
        "⚠️  Risks": "risks",
        "📌 Assumptions": "assumptions",
        "❓ Questions to Client": "questions_to_client",
    }

    for label, key in sections.items():
        items = analysis.get(key, [])
        print(f"\n{label} ({len(items)} items):")
        for i, item in enumerate(items, 1):
            print(f"  {i}. {item}")

    print("\n" + "="*60)


# ─────────────────────────────────────────────
# MAIN PIPELINE
# ─────────────────────────────────────────────
def main():
    print("="*60)
    print("  AI Requirements Analyzer — Powered by Groq LLM")
    print("="*60)

    # Step 1: Read requirements
    requirement_text = read_requirements(INPUT_FILE)

    # Step 2: Analyze with Groq
    analysis = analyze_with_groq(requirement_text)

    # Step 3: Print summary
    print_summary(analysis)

    # Step 4: Format email
    html_body = format_email_body(analysis, requirement_text)
    save_email_html(html_body)

    # Step 5: Send email
    sent = send_email(html_body, subject="📋 Requirements Analysis Report — Online Food Delivery Platform")

    print("\n" + "="*60)
    if sent:
        print("  ✅ Pipeline complete! Email sent successfully.")
    else:
        print("  ⚠️  Pipeline complete. Email NOT sent (check credentials).")
        print("      Review output/formatted_email.html to see the email.")
    print("="*60)


if __name__ == "__main__":
    main()