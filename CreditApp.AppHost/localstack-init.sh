#!/bin/bash

echo "Initializing LocalStack"

# Ожидание готовности LocalStack с проверкой
max_attempts=30
attempt=0
while [ $attempt -lt $max_attempts ]; do
    if awslocal sns list-topics > /dev/null 2>&1; then
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

# Создание SNS топика
awslocal sns create-topic --name credit-applications

# Подписка HTTP эндпоинта FileService на SNS топик
awslocal sns subscribe \
    --topic-arn arn:aws:sns:us-east-1:000000000000:credit-applications \
    --protocol http \
    --notification-endpoint http://host.docker.internal:5100/api/notification

echo "LocalStack initialization completed successfully"
