using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Bezalel.Aplication.Interface;
using Bezalel.Aplication.ViewModel;
using Bezalel.Core.DTOs;
using Bezalel.Core.Entities;
using Bezalel.Core.Interfaces;

namespace Bezalel.Aplication.Service;

/// <summary>
/// Orchestrates the creation and retrieval of CarouselJobs.
/// On creation: persists the job → publishes SQS message → returns 202.
/// On polling: reads from DynamoDB and returns the current state.
/// </summary>
public class CarouselService : ICarouselService
{
    private readonly ICarouselJobRepository _jobRepository;
    private readonly IQueuePublisher _queuePublisher;
    private readonly ISafetyService _safetyService;
    private readonly IAuditPublisher _auditPublisher;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CarouselService> _logger;

    public CarouselService(
        ICarouselJobRepository jobRepository,
        IQueuePublisher queuePublisher,
        ISafetyService safetyService,
        IAuditPublisher auditPublisher,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CarouselService> logger)
    {
        _jobRepository = jobRepository;
        _queuePublisher = queuePublisher;
        _safetyService = safetyService;
        _auditPublisher = auditPublisher;
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<ResultOperation> CreateAsync(CreateCarouselRequest request, string userId)
    {
        try
        {
            // Safety guardrail on user prompt
            if (!_safetyService.IsContentSafe(request.Tema))
            {
                _logger.LogWarning("[CarouselService] Prompt blocked by safety guardrails: {Tema}", request.Tema);
                return new ResultOperation { Success = false, Message = "O conteúdo fornecido viola nossas diretrizes de segurança." };
            }

            var job = new CarouselJob
            {
                JobId = Guid.NewGuid().ToString(),
                UserId = userId,
                Tema = request.Tema,
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            };

            // 1. Persist initial job record
            await _jobRepository.SaveAsync(job);

            // 2. Publish to SQS for CopywriterWorker processing
            var queueUrl = _configuration["AWS:CopywriterQueueUrl"];
            if (string.IsNullOrEmpty(queueUrl))
            {
                _logger.LogError("[CarouselService] AWS:CopywriterQueueUrl is not configured.");
                return new ResultOperation { Success = false, Message = "Configuração de fila inválida." };
            }

            var sqsMessage = new
            {
                job.JobId,
                job.Tema,
                SlideCount = request.SlideCount ?? 5,
                request.VisualStyle
            };

            await _queuePublisher.PublishAsync(sqsMessage, queueUrl);

            // 3. Audit log
            await _auditPublisher.PublishAsync(new AuditLogEntry
            {
                LogId = Guid.NewGuid().ToString(),
                Action = "CreateCarousel",
                UserId = userId,
                Details = $"Created carousel job {job.JobId} with theme: {request.Tema}",
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("[CarouselService] Job {JobId} created and published to SQS.", job.JobId);

            return new ResultOperation { Success = true, Data = new { job.JobId, job.Status } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CarouselService] Failed to create carousel job.");
            return new ResultOperation { Success = false, Message = ex.Message };
        }
    }

    public async Task<CarouselJob?> GetByIdAsync(string jobId)
    {
        return await _jobRepository.GetByIdAsync(jobId);
    }
}
