The Interactions API schema default will change on May 26, 2026.
 

Hello Anders,

You're receiving this message because you use the Interactions API. Starting May 26, 2026, we are transitioning the API schema from a flat output structure to a detailed, step-by-step timeline of interactions.

We understand this change may require some planning and have provided additional information below to help you with the transition. You can update your code today to the new schema.
What you need to know

Starting May 26, 2026, the Interactions API preview on v1beta will transition to a more structured schema to improve how model outputs are handled.

Key changes:

    Steps schema: The outputs array is replaced by a new steps array, providing a structured timeline of each interaction turn and updating streaming event names.
    Output format configuration: Output format controls are consolidated into a new polymorphic response_format, and response_mime_type is removed.

New future features will be only available on the new steps schema. See the migration guide for full technical details and code samples. We have prepared a migration skill that helps you make these code changes automatically using a coding agent.
What you need to do

To ensure a smooth transition, choose one of the following paths before May 26, 2026:

Option 1: Migrate now to the new schema (recommended)

    REST API: Add the Api-Revision header to your requests: Api-Revision: 2026-05-20
    SDK: Upgrade to the latest version, which opts in automatically:
        Python (≥2.0.0): pip install --upgrade google-genai
        JavaScript (≥2.0.0): npm install @google/genai@latest

Option 2: Pin to the current schema (temporary)

If you need more time, you must pin to the current schema before May 26, 2026 to avoid disruption. This opt-out is temporary; the legacy schema will be removed on June 8, 2026.

    REST API: Use the header: Api-Revision: 2026-05-07
    SDK: Pin to a legacy-compatible version (Python ≤1.75.0 or JS ≤1.52.0)

Timelines:

    May 7, 2026 (Opt-in phase)
        SDK Users: Upgrade to the latest version (Python ≥2.0.0 / JS ≥2.0.0) to receive the new schema automatically. These SDK versions will not support the legacy schema.
        REST API Users: Add the Api-Revision: 2026-05-20 header to opt in manually; the default remains the legacy schema.
    May 26, 2026 (Default flip)
        SDK Users: Older versions will continue to function but will return legacy responses.
        REST API Users: The new schema becomes the default. You must send the Api-Revision: 2026-05-07 header to opt out.
    June 8, 2026 (Sunset)
        SDK Users: Older SDK versions (Python 1.x.x and JS 1.x.x) will break for Interactions API calls.
        REST API Users: The legacy schema is permanently removed. The Api-Revision header will be ignored.

Impacted customers/accounts:

Your affected projects are listed below:

    gen-lang-client-0614360521

We're here to help

If you have any questions or require assistance with your migration, please visit the Google AI Forum.

Thanks for choosing Google AI Studio and Gemini API.

– The Google AI Studio Team
