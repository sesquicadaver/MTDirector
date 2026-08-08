#!/usr/bin/env python3
"""Verify Domain/Application coverage against Bootstrap Plan thresholds."""

from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


THRESHOLDS = {
    "Mfc.Domain": {"line": 0.85, "branch": 0.75},
    "Mfc.Application": {"line": 0.85, "branch": 0.75},
}


def is_generated_class(class_name: str) -> bool:
    return "RegexGenerator" in class_name or ".Generated." in class_name


def package_rates(cobertura: Path) -> dict[str, tuple[float, float, int]]:
    root = ET.parse(cobertura).getroot()
    rates: dict[str, tuple[float, float, int]] = {}
    for pkg in root.findall(".//package"):
        name = pkg.get("name") or ""
        short = name.split(",")[0]
        matched_key = None
        for key in THRESHOLDS:
            if short == key or short.endswith(key) or key in short:
                matched_key = key
                break
        if matched_key is None:
            continue

        lines: list[ET.Element] = []
        for cls in pkg.findall("classes/class"):
            class_name = cls.get("name") or ""
            if is_generated_class(class_name):
                continue
            lines.extend(cls.findall("lines/line"))

        if not lines:
            # Fall back to package attributes for empty assemblies.
            line_rate = float(pkg.get("line-rate") or "0")
            branch_rate = float(pkg.get("branch-rate") or "0")
            lines_valid = int(pkg.get("lines-valid") or "0")
            rates[matched_key] = (line_rate, branch_rate, lines_valid)
            continue

        hits = sum(1 for line in lines if int(line.get("hits") or "0") > 0)
        lines_valid = len(lines)
        line_rate = hits / lines_valid
        branch_rate = float(pkg.get("branch-rate") or "0")
        rates[matched_key] = (line_rate, branch_rate, lines_valid)
    return rates


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("coverage_root", type=Path)
    args = parser.parse_args()

    files = list(args.coverage_root.rglob("coverage.cobertura.xml"))
    if not files:
        print(f"ERROR: no coverage.cobertura.xml under {args.coverage_root}", file=sys.stderr)
        return 1

    aggregated: dict[str, tuple[float, float, int]] = {}
    for path in files:
        aggregated.update(package_rates(path))

    failed = False
    for assembly, thresholds in THRESHOLDS.items():
        if assembly not in aggregated:
            print(f"ERROR: coverage missing for {assembly}", file=sys.stderr)
            failed = True
            continue

        line_rate, branch_rate, lines_valid = aggregated[assembly]
        print(
            f"{assembly}: line={line_rate:.2%} branch={branch_rate:.2%} lines_valid={lines_valid}"
        )
        if lines_valid == 0:
            # Truly empty assembly: pass.
            continue
        if lines_valid < 5:
            # Marker-only bootstrap assemblies (e.g. Application before use-cases land).
            print(f"{assembly}: skipped threshold (marker-only, lines_valid={lines_valid})")
            continue
        if line_rate + 1e-9 < thresholds["line"]:
            print(
                f"ERROR: {assembly} line coverage {line_rate:.2%} < {thresholds['line']:.0%}",
                file=sys.stderr,
            )
            failed = True
        # Branch threshold applies when coverlet reports a meaningful branch rate.
        if branch_rate > 0 and branch_rate + 1e-9 < thresholds["branch"]:
            print(
                f"ERROR: {assembly} branch coverage {branch_rate:.2%} < {thresholds['branch']:.0%}",
                file=sys.stderr,
            )
            failed = True

    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
