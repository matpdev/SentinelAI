#!/bin/bash
set -e

awslocal sqs create-queue --queue-name sentinel-incident-dlq
DLQ_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url http://localhost:4566/000000000000/sentinel-incident-dlq \
  --attribute-names QueueArn \
  --query 'Attributes.QueueArn' \
  --output text)

awslocal sqs create-queue \
  --queue-name sentinel-incident-events \
  --attributes "{\"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$DLQ_ARN\\\",\\\"maxReceiveCount\\\":\\\"3\\\"}\"}"

awslocal s3 mb s3://sentinel-incident-snapshots

echo "LocalStack SQS queues and S3 bucket initialized."
