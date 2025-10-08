#!/usr/bin/env python3
"""
hello.py - simple executable script

Usage:
    ./hello.py <TaskName>

This script prints "Task <TaskName> completed" to stdout.
"""

import sys


def main():
    if len(sys.argv) < 2:
        print("Usage: ./hello.py <TaskName>")
        sys.exit(1)

    task_name = sys.argv[1]
    print(f"Task {task_name} completed")


if __name__ == "__main__":
    main()
