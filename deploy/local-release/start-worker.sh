#!/usr/bin/env bash
cd "$(dirname "$0")/Worker"
echo "Starting ColdNet Worker (background processing loop)."
echo "Press Ctrl+C to stop."
./ColdNet.Worker
