using System;
using System.Collections.Generic;

namespace StockDataMQClient
{
    /// <summary>
    /// 股票数据模型
    /// </summary>
    public class StockData
    {
        // 基本信息
        private string _code;
        public string Code
        {
            get { return _code; }
            set { _code = value; }
        }
        private string _name;
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }
        private UInt16 _marketType;
        public UInt16 MarketType
        {
            get { return _marketType; }
            set { _marketType = value; }
        }
        private int _tradeTime;
        public int TradeTime
        {
            get { return _tradeTime; }
            set { _tradeTime = value; }
        }

        // 价格数据
        private float _lastClose;
        public float LastClose
        {
            get { return _lastClose; }
            set { _lastClose = value; }
        }
        private float _open;
        public float Open
        {
            get { return _open; }
            set { _open = value; }
        }
        private float _high;
        public float High
        {
            get { return _high; }
            set { _high = value; }
        }
        private float _low;
        public float Low
        {
            get { return _low; }
            set { _low = value; }
        }
        private float _newPrice;
        public float NewPrice
        {
            get { return _newPrice; }
            set { _newPrice = value; }
        }

        // 成交数据
        private float _volume;
        public float Volume
        {
            get { return _volume; }
            set { _volume = value; }
        }
        private float _amount;
        public float Amount
        {
            get { return _amount; }
            set { _amount = value; }
        }

        // 计算数据
        private float _changePercent;
        public float ChangePercent
        {
            get { return _changePercent; }
            set { _changePercent = value; }
        }
        private float _changeAmount;
        public float ChangeAmount
        {
            get { return _changeAmount; }
            set { _changeAmount = value; }
        }

        // 时间戳
        private DateTime _updateTime;
        public DateTime UpdateTime
        {
            get { return _updateTime; }
            set { _updateTime = value; }
        }

        // 买卖盘数据（5档）
        private float[] _buyPrice;
        public float[] BuyPrice
        {
            get 
            { 
                if (_buyPrice == null)
                    _buyPrice = new float[5];
                return _buyPrice; 
            }
            set { _buyPrice = value; }
        }
        
        private float[] _buyVolume;
        public float[] BuyVolume
        {
            get 
            { 
                if (_buyVolume == null)
                    _buyVolume = new float[5];
                return _buyVolume; 
            }
            set { _buyVolume = value; }
        }
        
        private float[] _sellPrice;
        public float[] SellPrice
        {
            get 
            { 
                if (_sellPrice == null)
                    _sellPrice = new float[5];
                return _sellPrice; 
            }
            set { _sellPrice = value; }
        }
        
        private float[] _sellVolume;
        public float[] SellVolume
        {
            get 
            { 
                if (_sellVolume == null)
                    _sellVolume = new float[5];
                return _sellVolume; 
            }
            set { _sellVolume = value; }
        }

        /// <summary>
        /// 计算涨跌幅
        /// </summary>
        public void CalculateChangePercent()
        {
            if (LastClose > 0)
            {
                ChangePercent = ((NewPrice - LastClose) / LastClose) * 100;
            }
            else
            {
                ChangePercent = 0;
            }
        }

        /// <summary>
        /// 计算涨跌额
        /// </summary>
        public void CalculateChangeAmount()
        {
            ChangeAmount = NewPrice - LastClose;
        }

    }
}

