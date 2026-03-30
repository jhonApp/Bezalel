using Amazon.SQS;
using Amazon.SQS.Model;
using Bezalel.Core.DTOs;
using Bezalel.Core.Interfaces;
using System.Text.Json;

namespace Bezalel.Infrastructure.Services
{
    public class SqsAnalysisPublisher : ISqsPublisher
    {
        private readonly IAmazonSQS _sqsClient;
        private readonly string _queueUrl;

        public SqsAnalysisPublisher(IAmazonSQS sqsClient, string queueUrl)
        {
            _sqsClient = sqsClient;
            _queueUrl = queueUrl;
        }

        public async Task PublishToAnalysisQueueAsync(AnalysisJobMessage message)
        {
            var request = new SendMessageRequest
            {
                QueueUrl = _queueUrl,
                MessageBody = JsonSerializer.Serialize(message)
            };

            await _sqsClient.SendMessageAsync(request);
        }
    }
}
