# patch-suggestions/ — proposed fixes

When a bug in `../auto-bugs/BUG-<NNNN>-*.md` has an obvious fix, the flow-test
agent drops a suggested patch here, named to match the ticket:

```
BUG-<NNNN>-patch.cs     # backend C#
BUG-<NNNN>-patch.ts     # frontend TypeScript
BUG-<NNNN>-patch.sql    # schema / data
```

These are **suggestions, not auto-applied**. A patch is a focused snippet (or a
small diff-style block) showing the change, with a one-line header naming the
target file and method. The developer reviews, adapts, and applies it — keeping
the dev in control (the app is the source of truth, the agent assists).
