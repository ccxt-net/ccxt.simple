﻿namespace CCXT.Simple.Data
{
    /// <summary>
    ///
    /// </summary>
    public enum SideType : int
    {
        /// <summary>
        ///
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// bid
        /// </summary>
        Bid = 1,

        /// <summary>
        /// ask
        /// </summary>
        Ask = 2
    }

    /// <summary>
    ///
    /// </summary>
    public class SideTypeConverter
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static SideType FromString(string s)
        {
            switch (s.ToLower())
            {
                case "sell":
                case "short":
                case "offer":
                case "s":
                case "ask":
                case "1":
                    return SideType.Ask;

                case "buy":
                case "long":
                case "purchase":
                case "b":
                case "bid":
                case "0":
                    return SideType.Bid;

                default:
                    return SideType.Unknown;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static string ToString(SideType v)
        {
            return Enum.GetName(typeof(SideType), v).ToLowerInvariant();
        }
    }

}