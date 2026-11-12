#!/usr/bin/env python3
"""Fail the build when the number of compiler/analyzer warnings in a `dotnet build` log grows past the baseline.

Usage: check-warnings.py <build.log> <baseline-file> [tolerance-percent]

The baseline file holds one integer (the measured count). The tolerance defaults to 5%. Only compiler/analyzer codes (CA, CS, IDE, SYSLIB) count, not NuGet NU*. Warnings are de-duplicated
(msbuild prints each one once per target framework / node), so the count is stable between runs. The baseline is a
ratchet: lower it when the backlog shrinks, never raise it to make a build pass.
"""
import re
import sys

WARNING = re.compile(r"^(?P<loc>.+?): warning (?P<code>(?:CA|CS|IDE|SYSLIB)\d+): ")


def count_warnings(path):
    seen = set()
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            match = WARNING.match(line.strip())
            if match:
                seen.add((match.group("loc"), match.group("code"), line.strip().split(": warning ", 1)[1]))
    return len(seen)


def main(argv):
    if len(argv) < 3:
        print(__doc__)
        return 2
    tolerance = float(argv[3]) if len(argv) > 3 else 5.0
    with open(argv[2], encoding="utf-8") as handle:
        baseline = int(handle.read().strip())
    current = count_warnings(argv[1])
    limit = int(baseline * (1 + tolerance / 100))
    print(f"warnings: {current}, baseline: {baseline}, limit (+{tolerance:g}%): {limit}")
    if current == 0 and baseline > 0:
        print("::error::no warnings parsed - log format changed?")
        return 1
    if current < baseline:
        print(f"::notice::warnings dropped to {current} - lower {argv[2]} to {current} to keep the ratchet tight")
    if current > limit:
        print(f"::error::analyzer warnings grew to {current} (baseline {baseline}, limit {limit}) - fix the new warnings")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
