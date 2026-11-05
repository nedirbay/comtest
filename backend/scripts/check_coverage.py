#!/usr/bin/env python3
"""Merges the coverage of all test projects and fails below a line-coverage minimum.

Each test project writes its own report, and a line counts as covered if any of them ran it.

    check_coverage.py <results-directory> <minimum-percent>
"""
import pathlib
import sys
import xml.etree.ElementTree as ET


def main(directory: str, minimum: float) -> int:
    reports = list(pathlib.Path(directory).rglob("coverage.cobertura.xml"))

    if not reports:
        print(f"No coverage reports under {directory}")
        return 1

    lines: dict[tuple[str, str], bool] = {}

    for report in reports:
        for class_element in ET.parse(report).getroot().iter("class"):
            file_name = class_element.get("filename", "")

            for line in class_element.iter("line"):
                key = (file_name, line.get("number", ""))
                lines[key] = lines.get(key, False) or int(line.get("hits", "0")) > 0

    covered = sum(lines.values())
    percent = 100 * covered / len(lines) if lines else 0.0
    print(f"Line coverage: {percent:.1f}% ({covered}/{len(lines)}), minimum {minimum:g}%")

    return 0 if percent >= minimum else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1], float(sys.argv[2])))
