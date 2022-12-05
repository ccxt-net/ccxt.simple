using CCXT.Simple.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;

namespace CCXT.Simple.Exchanges.Upbit
{
    public class XUpbit : IExchange
    {
        /*
		 * Upbit Support Markets: KRW, USDT, BTC
		 *
		 * REST API
		 *     https://docs.upbit.com/reference
		 *     https://docs.upbit.com/docs/upbit-quotation-websocket
		 *     https://upbit.com/service_center/wallet_status
		 *
		 * EXCHANGE API
		 *     [주문 요청] 초당 8회, 분당 200회
		 *     [주문 요청 외 API] 초당 30회, 분당 900회
		 *
		 * QUOTATION API
		 *     Websocket 연결 요청 수 제한: 초당 5회, 분당 100회
		 *     REST API 요청 수 제한: 분당 600회, 초당 10회 (종목, 캔들, 체결, 티커, 호가별)
		 */

        public XUpbit(Exchange mainXchg, string apiKey = "", string secretKey = "", string passPhrase = "")
        {
            this.mainXchg = mainXchg;

            this.ApiKey = apiKey;
            this.SecretKey = secretKey;
            this.PassPhrase = passPhrase;
        }

        public Exchange mainXchg
        {
            get;
            set;
        }
        
        public string ExchangeName { get; set; } = "upbit";

        public string ExchangeUrl { get; set; } = "https://api.upbit.com";
        public string ExchangeUrlCc { get; set; } = "https://ccx.upbit.com";

        public bool Alive { get; set; }
        public string ApiKey { get; set; }
        public string SecretKey { get; set; }
        public string PassPhrase { get; set; }
        

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> VerifySymbols()
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _b_response = await _wc.GetAsync($"{ExchangeUrl}/v1/market/all?isDetails=true");
                    var _jstring = await _b_response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<List<CoinInfor>>(_jstring);

                    var _queue_info = this.mainXchg.GetXInfors(ExchangeName);

                    foreach (var s in _jarray)
                    {
                        var _symbol = s.market;

                        var _pairs = _symbol.Split('-');
                        if (_pairs.Length < 2)
                            continue;

                        var _base = _pairs[1];
                        var _quote = _pairs[0];

                        if (_quote == "KRW" || _quote == "BTC" || _quote == "USDT")
                        {
                            _queue_info.symbols.Add(new QueueSymbol
                            {
                                symbol = _symbol,
                                compName = _base,
                                baseName = _base,
                                quoteName = _quote
                            });
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 4201);
            }
            finally
            {
                this.Alive = _result;
            }

            return _result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> VerifyStates(Data.Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _b_response = await _wc.GetAsync($"{ExchangeUrlCc}/api/v1/status/wallet");
                    var _jstring = await _b_response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<List<CoinState>>(_jstring);

                    foreach (var c in _jarray)
                    {
                        var _currency = c.currency;

                        var _wallet_state = c.wallet_state;
                        var _block_state = c.block_state;
                        var _block_height = c.block_height != null ? c.block_height.Value : 0;
                        var _block_updated = c.block_updated_at != null ? c.block_updated_at.Value : DateTime.MinValue;
                        var _block_elapsed = c.block_elapsed_minutes != null ? c.block_elapsed_minutes.Value : 0;

                        // working, paused, withdraw_only, deposit_only, unsupported
                        var _active = _wallet_state != "unsupported";
                        var _deposit = _wallet_state == "working" || _wallet_state == "deposit_only";
                        var _withdraw = _wallet_state == "working" || _wallet_state == "withdraw_only";

                        var _state = tickers.states.SingleOrDefault(x => x.currency == _currency);
                        if (_state == null)
                        {
                            _state = new WState
                            {
                                currency = _currency,

                                active = _active,
                                deposit = _deposit,
                                withdraw = _withdraw,

                                height = _block_height,
                                updated = _block_updated,
                                elapsed = _block_elapsed,

                                travelRule = true,
                                networks = new List<WNetwork>()
                            };

                            tickers.states.Add(_state);
                        }
                        else
                        {
                            _state.active = _active;
                            _state.deposit = _deposit;
                            _state.withdraw = _withdraw;
                        }

                        var _t_items = tickers.items.Where(x => x.compName == _state.currency);
                        if (_t_items != null)
                        {
                            foreach (var t in _t_items)
                            {
                                t.active = _state.active;
                                t.deposit = _state.deposit;
                                t.withdraw = _state.withdraw;
                            }
                        }
                    }

                    _result = true;
                }

                this.mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 4202);
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 4203);
            }

            return _result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async ValueTask<decimal> GetPrice(string symbol)
        {
            var _result = 0.0m;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/v1/ticker?markets=" + symbol);
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<List<RaTicker>>(_jstring);

                    if (_jarray.Count > 0)
                        _result = _jarray[0].trade_price;
                }
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 4204);
            }

            return _result;
        }

        /// <summary>
        /// Get Upbit Tickers
        /// </summary>
        /// <param name="tickers"></param>
        /// <returns></returns>
        public async ValueTask<bool> GetTickers(Data.Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    var _request = String.Join(",", tickers.items.Where(x => x.symbol != "X").Select(x => x.symbol));

                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/v1/ticker?markets=" + _request);
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jmarkets = JsonConvert.DeserializeObject<List<RaTicker>>(_jstring);

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jmarkets.SingleOrDefault(x => x.market == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _price = _jitem.trade_price;

                            if (_ticker.symbol == "KRW-BTC")
                                this.mainXchg.OnKrwPriceEvent(_price);

                            if (_ticker.quoteName == "USDT")
                                _ticker.lastPrice = _price * tickers.exchgRate;
                            else if (_ticker.quoteName == "BTC")
                                _ticker.lastPrice = _price * mainXchg.krw_btc_price;
                            else
                                _ticker.lastPrice = _price;
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 4205);
            }

            return _result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="tickers"></param>
        /// <returns></returns>
        public async ValueTask<bool> GetVolumes(Data.Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    var _request = String.Join(",", tickers.items.Where(x => x.symbol != "X").Select(x => x.symbol));

                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/v1/ticker?markets=" + _request);
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jmarkets = JsonConvert.DeserializeObject<List<RaTicker>>(_jstring);

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jmarkets.SingleOrDefault(x => x.market == _ticker.symbol);
                        if (_jitem != null)
                        {
                            // UTC 0시 부터 누적 거래액
                            var _volume = _jitem.acc_trade_price;

                            var _prev_volume24h = _ticker.previous24h;
                            var _next_timestamp = _ticker.timestamp + 60 * 1000;

                            if (_ticker.quoteName == "USDT")
                                _volume *= tickers.exchgRate;
                            else if (_ticker.quoteName == "BTC")
                                _volume *= mainXchg.krw_btc_price;

                            _ticker.volume24h = Math.Floor(_volume / mainXchg.Volume24hBase);

                            var _curr_timestamp = _jitem.timestamp;
                            if (_curr_timestamp > _next_timestamp)
                            {
                                _ticker.volume1m = Math.Floor((_prev_volume24h > 0 ? _volume - _prev_volume24h : 0) / mainXchg.Volume1mBase);

                                _ticker.timestamp = _curr_timestamp;
                                _ticker.previous24h = _volume;
                            }
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 4206);
            }

            return _result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="tickers"></param>
        /// <returns></returns>
        public async ValueTask<bool> GetMarkets(Data.Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    var _request = String.Join(",", tickers.items.Where(x => x.symbol != "X").Select(x => x.symbol));

                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/v1/ticker?markets=" + _request);
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jmarkets = JsonConvert.DeserializeObject<List<RaTicker>>(_jstring);

                    foreach (var m in _jmarkets)
                    {
                        var _coin_name = m.market;

                        var _ticker = tickers.items.Find(x => x.symbol == _coin_name);
                        if (_ticker == null)
                            continue;

                        var _price = m.trade_price;
                        {
                            if (_ticker.quoteName == "KRW")
                            {
                                if (_coin_name == "KRW-BTC")
                                    this.mainXchg.OnKrwPriceEvent(_price);

                                _ticker.lastPrice = _price;
                            }
                            else if (_ticker.quoteName == "USDT")
                            {
                                _ticker.lastPrice = _price * tickers.exchgRate;
                            }
                            else if (_ticker.quoteName == "BTC")
                            {
                                _ticker.lastPrice = _price * mainXchg.krw_btc_price;
                            }
                        }

                        // UTC 0시 부터 누적 거래액
                        var _volume = m.acc_trade_price;
                        {
                            var _prev_volume24h = _ticker.previous24h;
                            var _next_timestamp = _ticker.timestamp + 60 * 1000;

                            if (_ticker.quoteName == "USDT")
                                _volume *= tickers.exchgRate;
                            else if (_ticker.quoteName == "BTC")
                                _volume *= mainXchg.krw_btc_price;

                            _ticker.volume24h = Math.Floor(_volume / mainXchg.Volume24hBase);

                            var _curr_timestamp = m.timestamp;
                            if (_curr_timestamp > _next_timestamp)
                            {
                                _ticker.volume1m = Math.Floor((_prev_volume24h > 0 ? _volume - _prev_volume24h : 0) / mainXchg.Volume1mBase);

                                _ticker.timestamp = _curr_timestamp;
                                _ticker.previous24h = _volume;
                            }
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 4207);
            }

            return _result;
        }

        ValueTask<bool> IExchange.GetBookTickers(Data.Tickers tickers)
        {
            throw new NotImplementedException();
        }
    }
}