---
ticket_id: BUG-0000
title: "[<area>] <one-line symptom>"
type: bug
priority: P0   # P0 blocker | P1 important | P2 follow-up
status: to-verify   # to-verify | in-progress | fixed | verified
detected_by: flow-test agent
detected_on: YYYY-MM-DD
detected_during: "/test-flow <flow-id> — <which check/indicator>"
affected_files:
  - "cinemabe/Cinema/2-Business/Cinema.Business/Managers/<File>.cs (<Method> L~NN)"
suggested_assignee: "<dev>"
related_flow: <flow-id>
related_check: <SC-XXX-NN / RI-XXX-NN>
---

## Description

<What is wrong and why it matters, in business terms. 1-3 short paragraphs.
State the invariant that is violated and the user-facing consequence.>

## Steps to reproduce

```
1. <action — API call / UI click, with concrete inputs>
2. <action>
ACTUAL: <what happens — status code, DB state, UI>
```

## Expected behavior

<What should happen instead. Include the assertion/guard that should hold.>

```csharp
// optional: the guard/shape that should be present
```

## Suggested fix

<Concrete direction — not necessarily final. Point at the file:line and the
change. If a patch file exists, reference ../patch-suggestions/BUG-NNNN-patch.<ext>.>

## Verification checklist

- [ ] <the static check that should now pass>
- [ ] <the xUnit test / playbook step that should now pass>
- [ ] Build green (dotnet build + dotnet test + ng build)
- [ ] No regression on sibling checks in the same flow
