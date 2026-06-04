---
name: model-recommendation
description: 'Analyze chatmode or prompt files and recommend the best currently available VS Code Copilot model based on complexity, capability fit, and cost.'
---

# AI Model Recommendation for Copilot Chat Modes and Prompts (Current VS Code Models)

## Mission

Analyze `.agent.md` or `.prompt.md` files and recommend the best model from the models currently available in VS Code Copilot Chat. Prioritize capability fit, output quality, and cost impact for the user's subscription tier.

## Core Rule

Do not rely on a static model list. Always verify availability and capabilities against current Copilot documentation before finalizing a recommendation.

## Scope and Preconditions

- Input: path to a `.agent.md` or `.prompt.md` file.
- Context: model availability can vary by date, tenant policy, region, and subscription tier.
- Tier assumption: Pro unless the user explicitly states Free or Pro+.

## Inputs

Required:

- `${input:filePath:Path to .agent.md or .prompt.md file}`

Optional:

- `${input:subscriptionTier:Pro}` (Free, Pro, Pro+)
- `${input:priorityFactor:Balanced}` (Speed, Cost, Quality, Balanced)
- `${input:allowAutoModel:true}` (true, false)

## 2026 Baseline Model Families

Use these as likely candidates, then verify exact names and multipliers at runtime.

| Family (Typical)        | Typical Strengths                             | Typical Trade-offs                         |
| ----------------------- | --------------------------------------------- | ------------------------------------------ |
| GPT-5.3-Codex class     | Code edits, refactors, precise implementation | Usually not best for vision-heavy prompts  |
| GPT-5.3 class           | Strong general reasoning and coding           | Slower and/or costlier than mini models    |
| GPT-5 mini class        | Fast and low-cost for routine tasks           | Weaker on deep multi-step reasoning        |
| Claude Sonnet 4.5 class | Strong reasoning, review, architecture        | Usually medium latency                     |
| Claude Opus class       | Deep analysis for highly complex tasks        | High multiplier or quota impact            |
| Gemini 2.5 Pro class    | Long context and multimodal analysis          | Can be slower for tight implementation loop |
| Gemini Flash class      | Speed-first responses and lightweight tasks   | Lower ceiling on difficult reasoning       |

If a family is not available in the user's tenant, exclude it and continue with available models only.

## Workflow

### 1. Analyze File Intent

- Parse frontmatter: `description`, `mode`, `tools`, `model`.
- Infer requirements:
  - Reasoning depth: basic, intermediate, advanced, expert.
  - Coding intensity: minimal, moderate, heavy.
  - Context need: small, medium, large, very large.
  - Modality: text-only vs multimodal.
  - Execution style: read-only analysis vs edit-validate loops.

### 2. Build Live Model Catalog

Before recommending, verify current model data.

Preferred source:

- `context7/get-library-docs` with `/websites/github_en_copilot`

Recommended queries:

- `current Copilot Chat models in VS Code`
- `request multipliers and billing for Copilot models`
- `which Copilot models support image analysis`
- `auto model selection behavior in Copilot Chat`

If live verification is unavailable:

- State that capability data is inferred from model-family behavior.
- Mark confidence as Medium.
- Avoid hard claims on exact multipliers.

### 3. Score Candidate Models

Score each available model from 1 to 5 on:

- Task fit
- Reasoning fit
- Code quality fit
- Speed fit
- Cost efficiency for tier
- Context capacity fit
- Modality fit (if needed)

Use weighted scoring by priority factor:

- Quality: reasoning 30%, code quality 25%, task fit 20%, context 15%, speed 5%, cost 5%
- Speed: speed 35%, task fit 20%, cost 20%, code quality 10%, reasoning 10%, context 5%
- Cost: cost 40%, task fit 20%, speed 20%, code quality 10%, reasoning 10%
- Balanced: task fit 25%, reasoning 20%, code quality 20%, speed 15%, cost 15%, context 5%

### 4. Generate Recommendation

Return all of the following:

- Primary model recommendation
- Two alternatives with trade-offs
- Auto-selection guidance (Recommended, Situational, Not recommended)
- Cost impact note for the selected tier
- Migration guidance if current model is unavailable or deprecated

## Quick Decision Heuristics

- Simple repetitive tasks: mini or flash class models.
- Daily implementation and refactoring: GPT-5.3-Codex class or GPT-5.3 class.
- Architecture, audits, and deep debugging: Claude Sonnet 4.5 class or GPT-5.3 class.
- Very long context or multimodal analysis: Gemini 2.5 Pro class.
- Highest-depth analysis with relaxed budget: Opus class.

## Output Format

Use the following report structure:

~~~markdown
# AI Model Recommendation Report

**File Analyzed**: [path]
**File Type**: [agent | prompt]
**Analysis Date**: [YYYY-MM-DD]
**Subscription Tier**: [Free | Pro | Pro+]
**Priority Factor**: [Speed | Cost | Quality | Balanced]

## File Summary
- Description: ...
- Mode: ...
- Tools: ...
- Current Model: ...

## Capability Requirements
- Reasoning: ...
- Coding Intensity: ...
- Context Need: ...
- Vision/Multimodal: ...

## Live Catalog Verification
- Source: [Context7 or inferred]
- Models considered: [...]
- Notes: [availability and multiplier caveats]

## Recommendation
### Primary: [Model]
- Why this fits: ...
- Cost profile: ...

### Alternatives
1. [Model] - Best when ...
2. [Model] - Best when ...

## Auto Model Selection
- Suitability: [Recommended | Situational | Not recommended]
- Manual override triggers: ...

## Frontmatter Update
~~~yaml
model: "[recommended-model-name]"
~~~
~~~

## Tool Alignment Rules

- If tools include heavy execution loops (`run*`, tests), avoid weakest speed-only models.
- If tools focus on architecture, review, or security, prioritize higher reasoning models.
- If task references images, diagrams, or screenshots, require verified vision support.
- If task is read-only search/fetch analysis, prioritize lower-cost and faster models.

## Quality Gates

- Recommendation must cite evidence from the analyzed file.
- Recommendation must include 1 primary model plus 2 alternatives.
- Recommendation must include cost implications for the selected tier.
- Confidence must be explicit:
  - High: live verified catalog
  - Medium: inferred from families

## Failure Conditions

- Invalid or unreadable path: request a valid `.agent.md` or `.prompt.md` path.
- Unclear task intent: ask one clarification question before final recommendation.
- No model metadata and no live catalog: provide provisional family-level recommendation.

## Practical Notes for VS Code

- Users can switch model from the Copilot Chat model picker.
- Auto mode is good for general workflows, but manual selection is better for specialized tasks.
- Model names may differ slightly by provider/version; match exact picker labels when writing frontmatter.

---

Last Updated: 2026-03-26
Model Data Policy: Live-verified first, static baseline second.
