#!/bin/bash

set -e

until dotnet ef database update; do
>&2 echo "SQLite is starting up"
sleep 1
done

>&2 echo "SQLite is up - executing command"
exec $run_cmd