using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using QuestLock.Core.Ipc;
using QuestLock.Core.Models;
using QuestLock.Core.Storage;

namespace QuestLock.Guardian.Services;

public class IpcServerService : BackgroundService
{
    private readonly StateManager _stateManager;
    private readonly ConfigManager _configManager;
    private readonly ILogger<IpcServerService> _logger;
    private readonly List<StreamWriter> _activeClients = new();
    private readonly object _clientsLock = new();

    public IpcServerService(StateManager stateManager, ConfigManager configManager, ILogger<IpcServerService> logger)
    {
        _stateManager = stateManager;
        _configManager = configManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IPC Named Pipe Server startuje na pipe: {PipeName}", IpcConstants.PipeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pipeServer = new NamedPipeServerStream(
                    IpcConstants.PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipeServer.WaitForConnectionAsync(stoppingToken);

                // Spustíme obsluhu klienta na pozadí, abychom mohli hned přijímat další spojení
                _ = HandleClientAsync(pipeServer, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chyba při čekání na IPC klienta");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipeServer, CancellationToken stoppingToken)
    {
        StreamWriter? writer = null;
        try
        {
            using (pipeServer)
            using (var reader = new StreamReader(pipeServer, Encoding.UTF8))
            {
                writer = new StreamWriter(pipeServer, new UTF8Encoding(false)) { AutoFlush = true };

                lock (_clientsLock)
                {
                    _activeClients.Add(writer);
                }

                _logger.LogInformation("Nový IPC klient připojen.");

                while (!stoppingToken.IsCancellationRequested && pipeServer.IsConnected)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break; // Spojení uzavřeno

                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var response = ProcessMessage(line);
                    if (response != null)
                    {
                        var responseJson = JsonSerializer.Serialize(response);
                        await writer.WriteLineAsync(responseJson);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "IPC klient se odpojil");
        }
        finally
        {
            if (writer != null)
            {
                lock (_clientsLock)
                {
                    _activeClients.Remove(writer);
                }
            }
            _logger.LogInformation("IPC klient byl odpojen.");
        }
    }

    private IpcMessage? ProcessMessage(string json)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<IpcMessage>(json);
            if (msg == null) return null;

            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var state = _stateManager.Load();
            var config = _configManager.Load();

            switch (msg.Type)
            {
                case IpcMessageTypes.GetInitData:
                {
                    var isCompletedToday = state.IsCompletedToday(today);
                    var payload = new InitDataPayload
                    {
                        IsLockedRequired = !isCompletedToday && state.IsLocked,
                        MathConfig = config.Math,
                        TodayDate = today
                    };
                    return new IpcMessage
                    {
                        Type = IpcMessageTypes.InitDataResponse,
                        Payload = JsonSerializer.Serialize(payload)
                    };
                }

                case IpcMessageTypes.SubmitCompletion:
                {
                    var submit = !string.IsNullOrEmpty(msg.Payload)
                        ? JsonSerializer.Deserialize<SubmitCompletionPayload>(msg.Payload)
                        : null;

                    var duration = submit?.DurationSeconds ?? 0;
                    _stateManager.MarkCompleted(today, duration);

                    _logger.LogInformation("Úkoly úspěšně splněny přes UI pro den {Today}.", today);

                    var payload = new UnlockGrantedPayload
                    {
                        UnlockedAt = DateTime.UtcNow,
                        UnlockedBy = "MATH_COMPLETION"
                    };
                    return new IpcMessage
                    {
                        Type = IpcMessageTypes.UnlockGranted,
                        Payload = JsonSerializer.Serialize(payload)
                    };
                }

                case IpcMessageTypes.Heartbeat:
                {
                    return new IpcMessage { Type = IpcMessageTypes.Heartbeat };
                }

                default:
                    return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chyba při zpracování IPC zprávy");
            return null;
        }
    }

    public async Task BroadcastUnlockAsync(string unlockedBy)
    {
        _logger.LogInformation("Vysílám UNLOCK signál všem připojeným IPC klientům ({UnlockedBy})...", unlockedBy);

        var payload = new UnlockGrantedPayload
        {
            UnlockedAt = DateTime.UtcNow,
            UnlockedBy = unlockedBy
        };

        var msg = new IpcMessage
        {
            Type = IpcMessageTypes.UnlockGranted,
            Payload = JsonSerializer.Serialize(payload)
        };

        var json = JsonSerializer.Serialize(msg);

        List<StreamWriter> clientsCopy;
        lock (_clientsLock)
        {
            clientsCopy = _activeClients.ToList();
        }

        foreach (var client in clientsCopy)
        {
            try
            {
                await client.WriteLineAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Chyba při odesílání zprávy klientovi");
            }
        }
    }

    public async Task BroadcastForceLockAsync()
    {
        _logger.LogInformation("Vysílám FORCE_LOCK signál všem připojeným IPC klientům...");

        var msg = new IpcMessage
        {
            Type = IpcMessageTypes.ForceLock
        };

        var json = JsonSerializer.Serialize(msg);

        List<StreamWriter> clientsCopy;
        lock (_clientsLock)
        {
            clientsCopy = _activeClients.ToList();
        }

        foreach (var client in clientsCopy)
        {
            try
            {
                await client.WriteLineAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Chyba při odesílání FORCE_LOCK zprávy klientovi");
            }
        }
    }
}
