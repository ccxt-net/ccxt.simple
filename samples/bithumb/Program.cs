using CCXT.Simple.Exchanges;
using CCXT.Simple.Exchanges.Bithumb;

var _exchange = new Exchange();

var _bithumb = new XBithumb(_exchange, "", "");
await _bithumb.VerifyCoinNames();

var _symbols = _exchange.GetSymbols(_bithumb.ExchangeName);
var _names = String.Join(",", _symbols.Select(x => x.symbol));

Console.WriteLine(_names);
Console.ReadLine();
