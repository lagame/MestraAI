using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Hubs;
using RPGSessionManager.Models;
using SixLabors.ImageSharp;
using System.Security.Cryptography;
using System.Text.Json;

namespace RPGSessionManager.Services
{
    public class MediaService : IMediaService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<BattlemapHub> _hubContext;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<MediaService> _logger;
        private readonly string _mediaPath;

        public MediaService(
            ApplicationDbContext context,
            IHubContext<BattlemapHub> hubContext,
            IWebHostEnvironment environment,
            ILogger<MediaService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _environment = environment;
            _logger = logger;
            
            // Criar diretório de mídia fora do webroot para segurança
            _mediaPath = Path.Combine(_environment.ContentRootPath, "Media");
            Directory.CreateDirectory(_mediaPath);
            Directory.CreateDirectory(Path.Combine(_mediaPath, "images"));
            Directory.CreateDirectory(Path.Combine(_mediaPath, "audio"));
            Directory.CreateDirectory(Path.Combine(_mediaPath, "video"));
        }

        // Substitua a criação do objeto Media em SaveMediaAsync para incluir a propriedade obrigatória 'Session'.
        // É necessário buscar a entidade Session correspondente ao sessionId antes de criar o objeto Media.

        public async Task<MediaInfo> SaveMediaAsync(IFormFile file, string mediaType, int sessionId, string uploadedBy)
        {
            // Gerar nome único para o arquivo
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var typeFolder = Path.Combine(_mediaPath, $"{mediaType}s");
            var filePath = Path.Combine(typeFolder, uniqueFileName);

            // Salvar arquivo fisicamente
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Extrair metadados
            var metadata = await ExtractMetadata(filePath, mediaType);

            // Buscar a entidade Session obrigatória
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null)
                throw new InvalidOperationException($"Sessão com ID {sessionId} não encontrada.");

            // Salvar no banco de dados
            var mediaEntity = new Media
            {
                FileName = uniqueFileName,
                OriginalFileName = file.FileName,
                MediaType = mediaType,
                FileSize = file.Length,
                FilePath = filePath,
                SessionId = sessionId,
                UploadedBy = uploadedBy,
                UploadedAt = DateTime.UtcNow,
                Metadata = JsonSerializer.Serialize(metadata),
                Session = session // Definindo o membro obrigatório
            };

            _context.Media.Add(mediaEntity);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Mídia salva: {uniqueFileName} ({file.Length} bytes) por {uploadedBy}");

            return new MediaInfo
            {
                Id = mediaEntity.Id,
                FileName = mediaEntity.FileName,
                OriginalFileName = mediaEntity.OriginalFileName,
                MediaType = mediaEntity.MediaType,
                FileSize = mediaEntity.FileSize,
                Url = $"/api/media/file/{mediaEntity.Id}",
                SessionId = mediaEntity.SessionId,
                UploadedBy = mediaEntity.UploadedBy,
                UploadedAt = mediaEntity.UploadedAt,
                Metadata = metadata
            };
        }

        public async Task<List<MediaInfo>> GetSessionMediaAsync(int sessionId, string? mediaType = null)
        {
            var query = _context.Media.Where(m => m.SessionId == sessionId);
            
            if (!string.IsNullOrEmpty(mediaType))
                query = query.Where(m => m.MediaType == mediaType);

            var mediaList = await query
                .OrderByDescending(m => m.UploadedAt)
                .ToListAsync();

            return mediaList.Select(m => new MediaInfo
            {
                Id = m.Id,
                FileName = m.FileName,
                OriginalFileName = m.OriginalFileName,
                MediaType = m.MediaType,
                FileSize = m.FileSize,
                Url = $"/api/media/file/{m.Id}",
                SessionId = m.SessionId,
                UploadedBy = m.UploadedBy,
                UploadedAt = m.UploadedAt,
                Metadata = string.IsNullOrEmpty(m.Metadata) ? null : 
                          JsonSerializer.Deserialize<Dictionary<string, object>>(m.Metadata)
            }).ToList();
        }

        public async Task<bool> DeleteMediaAsync(int mediaId, string requestedBy)
        {
            var media = await _context.Media.FindAsync(mediaId);
            if (media == null)
                return false;

            // Verificar permissões (apenas o uploader ou admin pode deletar)
            // TODO: Implementar verificação de roles
            if (media.UploadedBy != requestedBy)
            {
                _logger.LogWarning($"Tentativa de deletar mídia {mediaId} por usuário não autorizado: {requestedBy}");
                return false;
            }

            try
            {
                // Deletar arquivo físico
                if (File.Exists(media.FilePath))
                    File.Delete(media.FilePath);

                // Deletar do banco
                _context.Media.Remove(media);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Mídia deletada: {media.FileName} por {requestedBy}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao deletar mídia {mediaId}");
                return false;
            }
        }

        public async Task BroadcastAudioPlayAsync(int sessionId, int mediaId, float volume, bool loop)
        {
            var media = await _context.Media.FindAsync(mediaId);
            if (media == null || media.MediaType != "audio")
                return;

            var audioCommand = new
            {
                action = "play",
                mediaId = mediaId,
                url = $"/api/media/file/{mediaId}",
                volume = Math.Clamp(volume, 0, 100),
                loop = loop,
                fileName = media.OriginalFileName
            };

            await _hubContext.Clients.Group($"Session_{sessionId}")
                .SendAsync("AudioCommand", audioCommand);

            _logger.LogInformation($"Comando de áudio enviado para sessão {sessionId}: play {media.FileName}");
        }

        public async Task BroadcastAudioStopAsync(int sessionId, int? mediaId = null)
        {
            var audioCommand = new
            {
                action = "stop",
                mediaId = mediaId
            };

            await _hubContext.Clients.Group($"Session_{sessionId}")
                .SendAsync("AudioCommand", audioCommand);

            _logger.LogInformation($"Comando de parar áudio enviado para sessão {sessionId}");
        }

        public async Task<Media?> GetMediaByIdAsync(int mediaId)
        {
            return await _context.Media.FindAsync(mediaId);
        }

        private async Task<Dictionary<string, object>> ExtractMetadata(string filePath, string mediaType)
        {
            var metadata = new Dictionary<string, object>();

            try
            {
                var fileInfo = new FileInfo(filePath);
                metadata["fileSize"] = fileInfo.Length;
                metadata["createdAt"] = fileInfo.CreationTimeUtc;

                switch (mediaType)
                {
                    case "image":
                        await ExtractImageMetadata(filePath, metadata);
                        break;
                    case "audio":
                        await ExtractAudioMetadata(filePath, metadata);
                        break;
                    case "video":
                        await ExtractVideoMetadata(filePath, metadata);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Erro ao extrair metadados de {filePath}");
            }

            return metadata;
        }

        private async Task ExtractImageMetadata(string filePath, Dictionary<string, object> metadata)
        {
            try
            {
                using var image = await Image.LoadAsync(filePath);
                metadata["width"] = image.Width;
                metadata["height"] = image.Height;
                metadata["format"] = image.Metadata.DecodedImageFormat?.Name ?? "Unknown";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Erro ao extrair metadados de imagem: {filePath}");
            }
        }

        private Task ExtractAudioMetadata(string filePath, Dictionary<string, object> metadata)
        {
            metadata["type"] = "audio";
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                metadata["format"] = extension.TrimStart('.');
                // TODO: duração/bitrate via FFmpeg, etc.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Erro ao extrair metadados de áudio: {filePath}");
            }
            return Task.CompletedTask;
        }

        private Task ExtractVideoMetadata(string filePath, Dictionary<string, object> metadata)
        {
            metadata["type"] = "video";
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                metadata["format"] = extension.TrimStart('.');
                // TODO: resolução/duração/codec via FFmpeg, etc.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Erro ao extrair metadados de vídeo: {filePath}");
            }
            return Task.CompletedTask;
        }

    }
}
