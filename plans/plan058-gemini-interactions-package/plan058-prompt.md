---
type: prompt
description: "Original request for extracting Gemini Interactions client into its own package/workspace"
---

# Plan 058 Prompt: Gemini Interactions Package Extraction

**Created:** 2026-05-06T15:33:54+02:00

We have made our own Microsoft Agent Framework client for gemini (google) interactions api. This is now part of CatHerder.Agents.Core. Now we want to make that its own project/nuget package / workspace with its own test projects.

- One ordinar unit test project
- One integration test project that tests against real API.

The intention is to publish to github/nuget.

We have source code for Microsoft Agent Framework here: `/home/anders/source/agent/agent-framework`

It seems like their OpenAI client (or anthropic) is "reference" implementation. we want our project to mimic these, so that if someone is familiar with those clients (`Microsoft.Agents.AI.Anthropic`, `Microsoft.Agents.AI.OpenAI`) and their codebases they will feel at home with our client.

We also have this, which I originally intended to use but it seems like that is only for the old API (generative):

https://www.nuget.org/packages/Google_GenerativeAI.Microsoft/

But can maybe use as reference?

Step1: Create a plan for this.