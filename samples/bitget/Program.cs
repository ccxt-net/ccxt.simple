using Newtonsoft.Json.Linq;
using CCXT.Simple.Base;
using System.Net.WebSockets;
using System.Text;

namespace bitget;
class Program
{
    static async Task Main(string[] args)
    {
        var _p = new Program();
        await _p.Start(MainTokenSource.Token, new List<string> { "BTCUSDT", "ETHUSDT" });
    }

    private static CancellationTokenSource? __main_token_source;

    public static CancellationTokenSource MainTokenSource
    {
        get
        {
            if (__main_token_source == null)
                __main_token_source = new CancellationTokenSource();

            return __main_token_source;
        }
    }

    public async Task Start(CancellationToken token, List<string> symbols)
    {
        while (true)
        {
            var _buffer_size = 1024 * 16;
            var _buffer = new byte[_buffer_size];

            var _offset = 0;
            var _free = _buffer.Length;

            var _ping_timestamp = 0L;
            var _ping_sent = false;

            try
            {
                using (var _cws = new ClientWebSocket())
                {
                    await OpenWSAsync(token, _cws, symbols);

                    while (!token.IsCancellationRequested)
                    {
                        var _result = await _cws.ReceiveAsync(new ArraySegment<byte>(_buffer, _offset, _free), token);

                        _offset += _result.Count;
                        _free -= _result.Count;

                        if (_result.EndOfMessage == false)
                        {
                            if (_free == 0)
                            {
                                Array.Resize(ref _buffer, _buffer.Length + _buffer_size);
                                _free = _buffer.Length - _offset;
                            }

                            continue;
                        }

                        if (_result.MessageType == WebSocketMessageType.Close)
                        {
                            await _cws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", token);
                            Console.WriteLine($"receive close message from server...");

                            await Task.Delay(100);
                            break;
                        }

                        var _jstring = Encoding.UTF8.GetString(_buffer, 0, _offset);
                        Console.WriteLine(_jstring);

                        var _curr_timestamp = CUnixTime.Now;
                        {
                            if (_jstring == "pong")
                            {
                                _ping_timestamp = _curr_timestamp + 30;
                                _ping_sent = false;
                            }
                            else
                            {
                                if (_ping_timestamp < _curr_timestamp)
                                {
                                    if (!_ping_sent)
                                    {
                                        _ping_timestamp = _curr_timestamp + 30;
                                        _ping_sent = true;

                                        await SendAsync(token, _cws, "ping");
                                        Console.WriteLine("ping");
                                    }
                                    else if (_ping_timestamp + 30 < _curr_timestamp)
                                    {
                                        Console.WriteLine("close...");

                                        await _cws.CloseAsync(WebSocketCloseStatus.NormalClosure, "close web-socket", token);
                                        break;
                                    }
                                }
                            }
                        }

                        _offset = 0;
                        _free = _buffer.Length;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                await Task.Delay(1000 * 3);         // waiting for reconnect
            }
        }
    }

    private async ValueTask SendAsync(CancellationToken cancelToken, ClientWebSocket cws, string message)
    {
        var _cmd_bytes = Encoding.UTF8.GetBytes(message.Replace('\'', '\"'));
        await cws.SendAsync(
                    new ArraySegment<byte>(_cmd_bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken: cancelToken
                );
    }

    private async ValueTask OpenWSAsync(CancellationToken cancelToken, ClientWebSocket cws, List<string> symbols)
    {
        if (cws.State == WebSocketState.Open)
            await cws.CloseAsync(WebSocketCloseStatus.NormalClosure, "reopen", cancelToken);

        await cws.ConnectAsync(new Uri("wss://ws.bitget.com/spot/v1/stream"), cancelToken);

        var _args = String.Join(",", symbols.Select(x => $"{{'instType':'SP','channel':'ticker','instId':'{x}'}}"));

        var _message = "{'op':'subscribe', 'args':[" + _args + "]}";
        await this.SendAsync(cancelToken, cws, _message);
    }
}