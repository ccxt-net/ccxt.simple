using CCXT.Simple.Data;

namespace CCXT.Simple.Exchanges
{
    public interface IExchange
    {
        Exchange mainXchg { get; set; }

        //ExchangeTitle { get; set; }
        string ExchangeName { get; set; }
        string ExchangeUrl { get; set; }

        bool Alive { get; set; }

        string ApiKey { get; set; }
        string PassPhrase { get; set; }
        string SecretKey { get; set; }

        ValueTask<bool> VerifySymbols();
        ValueTask<bool> VerifyStates(Tickers tickers);
        
        ValueTask<decimal> GetPrice(string symbol);

        ValueTask<bool> GetBookTickers(Tickers tickers);
        ValueTask<bool> GetMarkets(Tickers tickers);
        ValueTask<bool> GetTickers(Tickers tickers);
        ValueTask<bool> GetVolumes(Tickers tickers);
    }
}