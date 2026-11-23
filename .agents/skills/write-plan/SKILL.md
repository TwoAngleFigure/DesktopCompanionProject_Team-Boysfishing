---
name: write-plan
description: Guidelines for writing a plan document. Use whenever the user asks to create, write, or draft a plan (계획서, 계획, plan) for a feature, system, or task.
---

# Guidelines for Writing a Plan

When requested to create a plan, you must strictly adhere to the following rules:

1. Ask questions if there is anything you do not know or are uncertain about.

2. Write the plan in the personal folder Docs/Plan located at the root level of the project (this folder is excluded from Git via .gitignore).

3. Name the plan using the format: 'Numbering + Plan Name'.

4. The plan must include the following sections:

- Overview
- Purpose
- Scenario
- Implementation Method
- Code Snippets
- Compatibility Review (e.g., consistency/alignment with the existing system)
- Project Impact Scope
- Application Method

5. Include at least one scenario in the plan. The scenario must describe the actual usage flow (e.g., a sequence of actions triggered by a specific event) and verify the feasibility of implementing that flow using the proposed structure.

6. The Scenario section MUST be placed immediately after the Purpose section, in the order listed in rule 4. Never place it in the latter part of the plan (e.g., after Code Snippets or at the end).
