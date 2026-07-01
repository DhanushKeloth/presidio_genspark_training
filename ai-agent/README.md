# Assignment: AI-Powered Requirements Analyzer
## Using Groq API (Free LLM) + Gmail SMTP

---

## 📋 Assignment Objective

Automate the process of reading a client requirement email, analyzing it using an LLM,
generating structured business analysis, and sending the result as a formatted email.

---

## 🏗️ Architecture

```
Client Email (.txt)
       ↓  [read]
  Python Script (requirements_analyzer.py)
       ↓  [HTTP POST]
   Groq API — LLaMA 3 70B (Free Tier)
       ↓  [JSON response]
  JSON Parser → saves analysis_output.json
       ↓  [format]
  HTML Email Formatter → saves formatted_email.html
       ↓  [smtplib SSL]
  Gmail SMTP (smtp.gmail.com:465)
       ↓  [sends]
  Recipient Inbox ✅
```

---

## 📦 Deliverables Checklist

| # | Deliverable | File |
|---|-------------|------|
| 1 | Prompt used | See PROMPT section below |
| 2 | Python Script | `requirements_analyzer.py` |
| 3 | Claude conversation | See CONVERSATION section below |
| 4 | Sample input | `input/client_requirements.txt` |
| 5 | Sample output | `output/analysis_output.json` |
| 6 | Email HTML | `output/formatted_email.html` |
| 7 | Architecture diagram | See diagram in report |

---

## 🔧 Setup Instructions

### 1. Install Dependencies
```bash
pip install groq
```

### 2. Get Groq API Key (Free)
1. Visit https://console.groq.com
2. Sign up for a free account
3. Go to API Keys → Create Key
4. Copy the key

### 3. Set Up Gmail App Password
1. Enable 2FA on your Google account
2. Go to: myaccount.google.com/apppasswords
3. Generate a new App Password (select "Mail")
4. Copy the 16-character password

### 4. Configure the Script
create a  `.env` and fill in:
```python
GROQ_API_KEY    = "gsk_xxxxxxxxxxxxxxxxxxxx"
GMAIL_SENDER    = "youremail@gmail.com"
GMAIL_APP_PASS  = "xxxx xxxx xxxx xxxx"
RECIPIENT_EMAIL = "recipient@example.com"
```

### 5. Run
```bash
python requirements_analyzer.py
```

---

## 💬 Prompt Used

```
You are a senior Business Analyst. Analyze the following client requirement email and 
return a structured JSON response with EXACTLY these 5 keys:

1. "functional_requirements"     - List of specific features the system must do
2. "non_functional_requirements" - List of quality attributes (performance, security, scalability)
3. "risks"                       - List of potential risks identified
4. "assumptions"                 - List of assumptions made while reading the requirements
5. "questions_to_client"         - List of clarifying questions to ask the client

Return ONLY valid JSON. No markdown, no explanation, no extra text.

CLIENT REQUIREMENT EMAIL:
"""
{requirement_text}
"""
```

**System Prompt:**
```
You are a senior Business Analyst. Always respond with valid JSON only.
```

---

## 🤖 Claude Conversation (Design Session)

**User:** I need to build a Python script that reads a client requirement email from a text 
file, uses an LLM to extract functional requirements, non-functional requirements, risks, 
assumptions, and questions to ask the client, then sends the result as a formatted HTML 
email via Gmail.

**Claude:** I'll design this as a 4-step pipeline:
1. Read the .txt file
2. Send to LLM (Groq) with a structured JSON prompt
3. Parse the JSON response into sections
4. Format as HTML email and send via Gmail SMTP

For the prompt, I'll instruct the LLM to return strictly valid JSON with exactly 5 keys. 
This avoids parsing issues. I'll use a system message to enforce JSON-only output and 
set temperature=0.3 for consistent, factual responses.

For Gmail, I'll use smtplib with SSL (port 465) and MIMEMultipart for HTML emails. 
The user needs a Gmail App Password (2FA required).

**User:** What model should I use on Groq for best results?

**Claude:** Use `llama3-70b-8192` — it's Groq's flagship free model with:
- 70 billion parameters (high reasoning quality)
- 8192 token context window (enough for long requirements)
- Very fast inference on Groq's LPU hardware
- Free tier: 14,400 requests/day, 6,000 tokens/minute

For business analysis tasks, LLaMA 3 70B consistently produces well-structured, 
professional outputs. Set temperature=0.3 to reduce hallucination.

**User:** How do I handle JSON parsing if the model returns extra text?

**Claude:** Add a regex fallback:
```python
try:
    analysis = json.loads(raw_text)
except json.JSONDecodeError:
    import re
    json_match = re.search(r'\{.*\}', raw_text, re.DOTALL)
    if json_match:
        analysis = json.loads(json_match.group())
```
This extracts JSON even if the model adds a preamble like "Here is the analysis:".

---

## 📥 Sample Input

**File:** `input/client_requirements.txt`

```
From: client@techcorp.com
Subject: Project Requirements - Online Food Delivery Platform

Hi Team,

We need to build an online food delivery platform similar to Swiggy/Zomato for our city.

The system should allow customers to browse restaurants, view menus, place orders, and 
track delivery in real-time. Restaurant owners should be able to manage their menu, view 
incoming orders, and update order status. Delivery agents should receive order assignments 
and update delivery status via a mobile app.

We want the app to be available on both Android and iOS, along with a web version.
Payment should support UPI, credit/debit cards, and cash on delivery.
We expect around 10,000 users at launch, scaling to 1 lakh users within 6 months.

We want the system to go live in 4 months. Our budget is around 15 lakhs INR.
The system must be secure, fast, and reliable. Downtime should be minimal.

Regards,
Rajesh Kumar — CTO, TechCorp Pvt. Ltd.
```

---

## 📤 Sample Output (analysis_output.json)

```json
{
  "functional_requirements": [
    "Customers can browse nearby restaurants by location",
    "Customers can view restaurant menus with item details and prices",
    "Customers can add items to cart and place orders",
    "Real-time order tracking on a map for customers",
    "Support for UPI, credit/debit card, and cash-on-delivery payments",
    "Restaurant owners can manage menu items (add/update/remove)",
    "Delivery agents receive order assignment notifications",
    "Multi-platform support: Android, iOS, and Web"
  ],
  "non_functional_requirements": [
    "Support 10,000 concurrent users at launch, scaling to 1,00,000 in 6 months",
    "System availability (uptime) must be 99.9% or higher",
    "All payment transactions must be PCI-DSS compliant",
    "APIs must respond within 200ms for 95% of requests"
  ],
  "risks": [
    "4-month timeline is aggressive for a full-stack multi-platform platform",
    "15 lakh budget may be insufficient for full scope",
    "Scaling from 10K to 1 lakh users requires proactive infrastructure planning"
  ],
  "assumptions": [
    "Platform serves a single city initially",
    "Payment gateway will use Razorpay or PayU",
    "Cloud infrastructure cost is within the 15 lakh budget"
  ],
  "questions_to_client": [
    "Is the 4-month deadline fixed or flexible?",
    "Does the 15 lakh budget include infrastructure and API costs?",
    "Which payment gateway do you prefer?",
    "Should the platform support scheduled orders?"
  ]
}
```

---

## 📧 Console Output (when script runs)

```
============================================================
  AI Requirements Analyzer — Powered by Groq LLM
============================================================
[1] Reading requirements from: input/client_requirements.txt
    ✓ Read 842 characters

[2] Sending to Groq LLM for analysis...
    ✓ Groq responded (1847 characters)
    ✓ Analysis saved to: output/analysis_output.json

============================================================
           REQUIREMENTS ANALYSIS SUMMARY
============================================================

✅ Functional Requirements (17 items):
  1. Customers can register, login, and manage their profile
  2. Customers can browse nearby restaurants by location
  ...

[3] Formatting email body...
    ✓ Email formatted
    ✓ Email HTML saved to: output/formatted_email.html

[4] Sending email to: recipient@example.com
    ✓ Email sent successfully to recipient@example.com

============================================================
  ✅ Pipeline complete! Email sent successfully.
============================================================
```

---

## ⚙️ Dependencies

```
groq>=0.9.0
Python standard library: smtplib, json, os, re, email
```

Install: `pip install groq`

---

## 🔑 Environment Variables (Optional — safer than hardcoding)

```bash
export GROQ_API_KEY="gsk_xxx"
export GMAIL_SENDER="you@gmail.com"
export GMAIL_APP_PASS="xxxx xxxx xxxx xxxx"
export RECIPIENT_EMAIL="recipient@example.com"
```

Then in script: `GROQ_API_KEY = os.environ.get("GROQ_API_KEY", "fallback")`