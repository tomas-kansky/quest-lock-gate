using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using QuestLock.Core.Ipc;
using QuestLock.Core.Models;

namespace QuestLock.UI.Services;

public class IpcClientService : IDisposable
{
    private NamedPipeClientStream? _pipeClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    public event Action<UnlockGrantedPayload>? OnUnlockGranted;
    public event Action? OnForceLockRequested;
    public event Action<bool>? OnConnectionStatusChanged;

    public bool IsConnected => _pipeClient?.IsConnected ?? false;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => ConnectionLoopAsync(_cts.Token));
    }

    private async Task ConnectionLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                _pipeClient = new NamedPipeClientStream(".", IpcConstants.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await _pipeClient.ConnectAsync(3000, token);

                _reader = new StreamReader(_pipeClient, Encoding.UTF8);
                _writer = new StreamWriter(_pipeClient, new UTF8Encoding(false)) { AutoFlush = true };

                OnConnectionStatusChanged?.Invoke(true);

                // Čtení zpráv ze služby
                while (!token.IsCancellationRequested && _pipeClient.IsConnected)
                {
                    var line = await _reader.ReadLineAsync();
                    if (line == null) break;

                    if (string.IsNullOrWhiteSpace(line)) continue;

                    ProcessIncomingMessage(line);
                }
            }
            catch
            {
                // Služba ještě neběží nebo spojení spadlo
            }
            finally
            {
                CleanupConnection();
                OnConnectionStatusChanged?.Invoke(false);
            }

            await Task.Delay(2500, token);
        }
    }

    private void ProcessIncomingMessage(string json)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<IpcMessage>(json);
            if (msg == null) return;

            if (msg.Type == IpcMessageTypes.UnlockGranted)
            {
                var payload = !string.IsNullOrEmpty(msg.Payload)
                    ? JsonSerializer.Deserialize<UnlockGrantedPayload>(msg.Payload)
                    : new UnlockGrantedPayload();

                OnUnlockGranted?.Invoke(payload ?? new UnlockGrantedPayload());
            }
            else if (msg.Type == IpcMessageTypes.ForceLock)
            {
                OnForceLockRequested?.Invoke();
            }
        }
        catch
        {
            // Ignorovat vadné zprávy
        }
    }

    public async Task<InitDataPayload?> RequestInitDataAsync()
    {
        if (!IsConnected || _writer == null || _reader == null) return null;

        try
        {
            var request = new IpcMessage { Type = IpcMessageTypes.GetInitData };
            var json = JsonSerializer.Serialize(request);

            await _sendSemaphore.WaitAsync();
            try
            {
                await _writer.WriteLineAsync(json);
            }
            finally
            {
                _sendSemaphore.Release();
            }

            // Očekáváme odpověď
            var responseJson = await _reader.ReadLineAsync();
            if (string.IsNullOrEmpty(responseJson)) return null;

            var response = JsonSerializer.Deserialize<IpcMessage>(responseJson);
            if (response?.Type == IpcMessageTypes.InitDataResponse && !string.IsNullOrEmpty(response.Payload))
            {
                return JsonSerializer.Deserialize<InitDataPayload>(response.Payload);
            }
        }
        catch
        {
            // Fallback při chybě
        }

        return null;
    }

    public async Task SubmitCompletionAsync(int correct, int total, int durationSeconds)
    {
        if (!IsConnected || _writer == null) return;

        try
        {
            var payload = new SubmitCompletionPayload
            {
                CorrectAnswers = correct,
                TotalQuestions = total,
                DurationSeconds = durationSeconds
            };

            var msg = new IpcMessage
            {
                Type = IpcMessageTypes.SubmitCompletion,
                Payload = JsonSerializer.Serialize(payload)
            };

            var json = JsonSerializer.Serialize(msg);
            await _sendSemaphore.WaitAsync();
            try
            {
                await _writer.WriteLineAsync(json);
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }
        catch
        {
            // Fallback
        }
    }

    private void CleanupConnection()
    {
        try { _reader?.Dispose(); } catch { }
        try { _writer?.Dispose(); } catch { }
        try { _pipeClient?.Dispose(); } catch { }
        _reader = null;
        _writer = null;
        _pipeClient = null;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        CleanupConnection();
    }
}
