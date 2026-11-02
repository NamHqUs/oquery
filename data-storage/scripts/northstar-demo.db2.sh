#!/bin/bash
set -e
su - db2inst1 -c 'db2 connect to NORTHSTA; db2 -tvf /scripts/northstar-demo.db2.sql'
