module Workflows_Step02_EdgeCondition.Emails

let spam = """Subject: 🎉 CONGRATULATIONS! You've WON $1,000,000 - CLAIM NOW! 🎉

Dear Valued Customer,

URGENT NOTICE: You have been selected as our GRAND PRIZE WINNER!

🏆 YOU HAVE WON $1,000,000 USD 🏆

This is NOT a joke! You are one of only 5 lucky winners selected from millions of email addresses worldwide.

To claim your prize, you MUST respond within 24 HOURS or your winnings will be forfeited!

CLICK HERE NOW: http://win-claim.com

What you need to do:
1. Reply with your full name
2. Provide your bank account details
3. Send a processing fee of $500 via wire transfer

ACT FAST! This offer expires TONIGHT at midnight!

Best regards,
Dr. Johnson Williams
International Lottery Commission
Phone: +1-555-999-1234
"""

let legitimate = """
Subject: Team Meeting Follow-up - Action Items

Hi Sarah,

I wanted to follow up on our team meeting this morning and share the action items we discussed:

1. Update the project timeline by Friday
2. Schedule client presentation for next week
3. Review the budget allocation for Q4

Please let me know if you have any questions or if I missed anything from our discussion.

Best regards,
Alex Johnson
Project Manager
Tech Solutions Inc.
alex.johnson@techsolutions.com
(555) 123-4567
"""

let ambigious = """
Subject: Action Required: Verify Your Account

Dear Valued Customer,

We have detected unusual activity on your account and need to verify your identity to ensure your security.

To maintain access to your account, please login to your account and complete the verification process.

Account Details:
- User: johndoe@contoso.com
- Last Login: 08/15/2025
- Location: Seattle, WA
- Device: Mobile

This is an automated security measure. If you believe this email was sent in error, please contact our support team immediately.

Best regards,
Security Team
Customer Service Department
"""