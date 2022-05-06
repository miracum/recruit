#!/bin/sh

export PGUSER="$DATASOURCE_USERNAME"
export PGPASSWORD="$DATASOURCE_PASSWORD"

until psql -c "select 1"; do
	echo "$(date): Waiting for ${PGHOST} to be up"
	sleep 30
done

catalina.sh run
