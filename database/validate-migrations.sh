#!/usr/bin/env bash

set -euo pipefail

MIGRATION_DIR="database/migrations"

echo "Checking migrations..."

if [ ! -d "$MIGRATION_DIR" ]; then
    echo "Migration directory not found: $MIGRATION_DIR"
    exit 1
fi

files=$(find "$MIGRATION_DIR" -maxdepth 1 -type f -name "*.sql" | sort)

if [ -z "$files" ]; then
    echo "No SQL migrations found."
    exit 1
fi

previous=""

for file in $files; do
    name=$(basename "$file")

    echo "Checking: $name"

    if [ ! -s "$file" ]; then
        echo "Migration is empty: $file"
        exit 1
    fi

    if [[ ! "$name" =~ ^[0-9]{3}_.+\.sql$ ]]; then
        echo "Invalid migration filename: $name"
        echo "Expected format: 001_name.sql"
        exit 1
    fi

    current="${name:0:3}"

    if [ -n "$previous" ] && [ "$current" = "$previous" ]; then
        echo "Duplicate migration number: $current"
        exit 1
    fi

    previous="$current"
done

echo "Migration validation passed."