#!/usr/bin/env python3
"""
hello.py - simple executable script

Usage:
    ./hello.py <TaskTitle>

This script prints "Task <TaskTitle> completed" to stdout.
"""

import sys


def main():
    if len(sys.argv) < 2:
        print("Usage: ./hello.py <TaskTitle>")
        sys.exit(1)

    task_title = sys.argv[1]
    print(f"Task completed")


if __name__ == "__main__":
    main()
