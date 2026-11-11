#!/usr/bin/env python3
"""Fail the build when a package's line coverage in a Cobertura report is below a threshold.

Usage: check-coverage.py <threshold-percent> <package-name> <report.xml> [<report.xml> ...]

The threshold is a ratchet: set it just under the current figure and raise it as coverage grows. When
several reports are given (one per test run), the best figure for the package is used.
"""
import sys
import xml.etree.ElementTree as ET


def package_line_rate(path, package):
    root = ET.parse(path).getroot()
    for pkg in root.iter("package"):
        if pkg.get("name") == package:
            return float(pkg.get("line-rate")) * 100
    return None


def main(argv):
    if len(argv) < 4:
        print(__doc__)
        return 2
    threshold = float(argv[1])
    package = argv[2]
    rates = [(p, package_line_rate(p, package)) for p in argv[3:]]
    found = [(p, r) for p, r in rates if r is not None]
    if not found:
        print(f"::error::package '{package}' not found in any coverage report: {argv[3:]}")
        return 1
    path, best = max(found, key=lambda x: x[1])
    print(f"{package}: line coverage {best:.1f}% (threshold {threshold:.1f}%) from {path}")
    if best < threshold:
        print(f"::error::line coverage of {package} dropped below {threshold:.1f}%")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
