#!/bin/bash

echo "Initializing LocalStack"

max_attempts=30
attempt=0
while [ $attempt -lt $max_attempts ]; do
    if awslocal sqs list-queues > /dev/null 2>&1; then
        echo "LocalStack is ready!"
        break
    fi
    attempt=$((attempt + 1))
    echo "Waiting for LocalStack (attempt $attempt/$max_attempts)"
    sleep 1
done

if [ $attempt -eq $max_attempts ]; then
    echo "ERROR: LocalStack failed to start"
    exit 1
fi

awslocal sqs create-queue --queue-name credit-applications

echo "LocalStack initialization completed successfully"
