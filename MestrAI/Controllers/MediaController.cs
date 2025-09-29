using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RPGSessionManager.Dtos;
using RPGSessionManager.Services;

namespace RPGSessionManager.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MediaController : ControllerBase
    {
        private readonly IMediaService _mediaService;
        private readonly ILogger<MediaController> _logger;
        private readonly IPermissionService _permissionService;
        private readonly IUserResolverService _userResolverService;
        // Configurações de segurança
        private static readonly Dictionary<string, string[]> AllowedMimeTypes = new()
        {
            ["image"] = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif" },
            ["audio"] = new[] { "audio/mpeg", "audio/mp3", "audio/wav", "audio/ogg", "audio/mp4" },
            ["video"] = new[] { "video/mp4", "video/webm" }
        };
        
        private static readonly Dictionary<string, long> MaxFileSizes = new()
        {
            ["image"] = 10 * 1024 * 1024,  // 10 MB
            ["audio"] = 50 * 1024 * 1024,  // 50 MB
            ["video"] = 100 * 1024 * 1024  // 100 MB
        };
        
        private static readonly string[] DangerousExtensions = 
        {
            ".exe", ".bat", ".cmd", ".com", ".pif", ".scr", ".vbs", ".js", ".jar", ".php", ".asp", ".aspx"
        };

        public MediaController(IMediaService mediaService, ILogger<MediaController> logger, IPermissionService permissionService, IUserResolverService userResolverService)
        {
            _mediaService = mediaService;
            _logger = logger;
            _permissionService = permissionService;
            _userResolverService = userResolverService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadMedia([FromForm] MediaUploadRequest request)
        {
            try
            {
                // Validação básica
                if (request.FileName == null || request.FileName.Length == 0)
                    return BadRequest(new { error = "Nenhum arquivo foi enviado." });

                if (string.IsNullOrEmpty(request.MediaType))
                    return BadRequest(new { error = "Tipo de mídia é obrigatório." });

                // Validação de segurança
                var validationResult = await ValidateFile(request.FileName, request.MediaType);
                if (!validationResult.IsValid)
                    return BadRequest(new { error = validationResult.Errors });

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                // Verifica se o usuário tem acesso à sessão para a qual está enviando o arquivo
                if (!await _permissionService.CanAccessSessionAsync(userId, request.SessionId))
                {
                  _logger.LogWarning("Usuário {UserId} tentou fazer upload para a sessão {SessionId} sem permissão.", userId, request.SessionId);
                  return Forbid(); // Retorna 403 Forbidden
                }

                // Processar upload
                var mediaInfo = await _mediaService.SaveMediaAsync(
                    request.FileName,
                    request.MediaType,
                    request.SessionId,
                    userId
                );

                _logger.LogInformation($"Mídia uploaded: {mediaInfo.Id} by {User.Identity?.Name}");

                return Ok(new
                {
                    id = mediaInfo.Id,
                    fileName = mediaInfo.FileName,
                    mediaType = mediaInfo.MediaType,
                    fileSize = mediaInfo.FileSize,
                    url = mediaInfo.Url,
                    uploadedAt = mediaInfo.UploadedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante upload de mídia");
                return StatusCode(500, new { error = "Erro interno do servidor." });
            }
        }

        [HttpGet("session/{sessionId}")]
        public async Task<IActionResult> GetSessionMedia(int sessionId, [FromQuery] string? mediaType = null)
        {
            try
            {
                var media = await _mediaService.GetSessionMediaAsync(sessionId, mediaType);
                return Ok(media);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao buscar mídias da sessão {sessionId}");
                return StatusCode(500, new { error = "Erro interno do servidor." });
            }
        }

        [HttpDelete("{mediaId}")]
        public async Task<IActionResult> DeleteMedia(int mediaId)
        {
            try
            {
                var userId = _userResolverService.GetUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Forbid(); // ou Unauthorized()
                }
                var success = await _mediaService.DeleteMediaAsync(mediaId, userId);
                if (!success)
                    return NotFound(new { error = "Mídia não encontrada ou sem permissão." });

                return Ok(new { message = "Mídia removida com sucesso." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao deletar mídia {mediaId}");
                return StatusCode(500, new { error = "Erro interno do servidor." });
            }
        }

        [HttpPost("audio/play")]
        public async Task<IActionResult> PlayAudio([FromBody] PlayAudioRequest request)
        {
            try
            {
                await _mediaService.BroadcastAudioPlayAsync(request.SessionId, request.MediaId, request.Volume, request.Loop);
                return Ok(new { message = "Áudio iniciado para todos os jogadores." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao reproduzir áudio");
                return StatusCode(500, new { error = "Erro interno do servidor." });
            }
        }

        [HttpPost("audio/stop")]
        public async Task<IActionResult> StopAudio([FromBody] StopAudioRequest request)
        {
            try
            {
                await _mediaService.BroadcastAudioStopAsync(request.SessionId, request.MediaId);
                return Ok(new { message = "Áudio parado para todos os jogadores." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao parar áudio");
                return StatusCode(500, new { error = "Erro interno do servidor." });
            }
        }

        [HttpGet("file/{mediaId}")]
        [AllowAnonymous] // Permitir acesso aos arquivos sem autenticação para facilitar o uso
        public async Task<IActionResult> GetMediaFile(int mediaId)
        {
            try
            {
                var media = await _mediaService.GetMediaByIdAsync(mediaId);
                if (media == null)
                    return NotFound();

                if (!System.IO.File.Exists(media.FilePath))
                    return NotFound();

                var fileBytes = await System.IO.File.ReadAllBytesAsync(media.FilePath);
                return File(fileBytes, media.MediaType == "image" ? "image/*" : 
                                     media.MediaType == "audio" ? "audio/*" : 
                                     media.MediaType == "video" ? "video/*" : "application/octet-stream", 
                           media.OriginalFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao servir arquivo de mídia {mediaId}");
                return StatusCode(500);
            }
        }

        private async Task<Dtos.ValidationResult> ValidateFile(IFormFile file, string mediaType)
        {
            // 1. Validar tipo de mídia
            if (!AllowedMimeTypes.ContainsKey(mediaType))
                return Dtos.ValidationResult.Fail(new[] { "Tipo de mídia não suportado." });

            // 2. Validar tamanho do arquivo
            if (file.Length > MaxFileSizes[mediaType])
                return Dtos.ValidationResult.Fail(new[] { $"Arquivo muito grande. Máximo permitido: {MaxFileSizes[mediaType] / (1024 * 1024)} MB." });

            // 3. Validar extensão do arquivo
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (DangerousExtensions.Contains(extension))
                return Dtos.ValidationResult.Fail(new[] { "Tipo de arquivo não permitido por motivos de segurança." });

            // 4. Validar MIME type
            if (!AllowedMimeTypes[mediaType].Contains(file.ContentType.ToLowerInvariant()))
                return Dtos.ValidationResult.Fail(new[] { "Tipo de arquivo não corresponde ao esperado." });

            // 5. Validar nome do arquivo
            if (!IsValidFileName(file.FileName))
                return Dtos.ValidationResult.Fail(new[] { "Nome do arquivo contém caracteres inválidos." });

            // 6. Validar magic numbers (cabeçalho do arquivo)
            var isValidHeader = await ValidateFileHeader(file, mediaType);
            if (!isValidHeader)
                return Dtos.ValidationResult.Fail(new[] { "Arquivo corrompido ou tipo inválido." });

            return Dtos.ValidationResult.Success();
        }

        private static bool IsValidFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            // Remover caracteres perigosos
            var invalidChars = Path.GetInvalidFileNameChars();
            var dangerousPatterns = new[] { "..", "/", "\\", ":", "*", "?", "\"", "<", ">", "|" };
            
            return !fileName.Any(c => invalidChars.Contains(c)) && 
                   !dangerousPatterns.Any(pattern => fileName.Contains(pattern));
        }

        private static async Task<bool> ValidateFileHeader(IFormFile file, string mediaType)
        {
            using var stream = file.OpenReadStream();
            var buffer = new byte[16];
            await stream.ReadAsync(buffer, 0, buffer.Length);
            stream.Position = 0;

            return mediaType switch
            {
                "image" => ValidateImageHeader(buffer),
                "audio" => ValidateAudioHeader(buffer),
                "video" => ValidateVideoHeader(buffer),
                _ => false
            };
        }

        private static bool ValidateImageHeader(byte[] buffer)
        {
            // JPEG
            if (buffer.Length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xD8)
                return true;
            
            // PNG
            if (buffer.Length >= 8 && buffer[0] == 0x89 && buffer[1] == 0x50 && 
                buffer[2] == 0x4E && buffer[3] == 0x47)
                return true;
            
            // GIF
            if (buffer.Length >= 6 && buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46)
                return true;
            
            // WebP
            if (buffer.Length >= 12 && buffer[0] == 0x52 && buffer[1] == 0x49 && 
                buffer[2] == 0x46 && buffer[3] == 0x46 && buffer[8] == 0x57 && 
                buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50)
                return true;

            return false;
        }

        private static bool ValidateAudioHeader(byte[] buffer)
        {
            // MP3
            if (buffer.Length >= 3 && buffer[0] == 0xFF && (buffer[1] & 0xE0) == 0xE0)
                return true;
            
            // WAV
            if (buffer.Length >= 12 && buffer[0] == 0x52 && buffer[1] == 0x49 && 
                buffer[2] == 0x46 && buffer[3] == 0x46 && buffer[8] == 0x57 && 
                buffer[9] == 0x41 && buffer[10] == 0x56 && buffer[11] == 0x45)
                return true;
            
            // OGG
            if (buffer.Length >= 4 && buffer[0] == 0x4F && buffer[1] == 0x67 && 
                buffer[2] == 0x67 && buffer[3] == 0x53)
                return true;

            return false;
        }

        private static bool ValidateVideoHeader(byte[] buffer)
        {
            // MP4
            if (buffer.Length >= 8 && buffer[4] == 0x66 && buffer[5] == 0x74 && 
                buffer[6] == 0x79 && buffer[7] == 0x70)
                return true;
            
            // WebM
            if (buffer.Length >= 4 && buffer[0] == 0x1A && buffer[1] == 0x45 && 
                buffer[2] == 0xDF && buffer[3] == 0xA3)
                return true;

            return false;
        }
    }    
}

