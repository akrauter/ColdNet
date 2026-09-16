#!/usr/bin/env bash
cd "$(dirname "$0")/Admin"
echo "Starting ColdNet Admin - open http://localhost:5202 in your browser once it says \"Application started\"."
echo "Press Ctrl+C to stop."
./ColdNet.Admin
