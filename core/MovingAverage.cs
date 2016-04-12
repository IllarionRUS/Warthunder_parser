using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace WarThunderParser
{
    public class MovingAverage
    {
        public int Period = 5;
        public double Alpha = 0.5;
        private readonly Queue<double> _quotes = new Queue<double>();

        public MovingAverage(int period)
        {
            Period = period;
        }
        public void Push(double quote)
        {
            if (_quotes.Count == Period)
                _quotes.Dequeue();
            _quotes.Enqueue(quote);

        }
        public void Clear()
        {
            _quotes.Clear();
            
        }
        public double Average { get { if (_quotes.Count == 0) return 0; return _quotes.Average(); } }
        public double ExponentialMovingAverage 
        {
            get
            {
                return  _quotes.DefaultIfEmpty().Aggregate((ema, nextQuote) => Alpha * nextQuote + (1 - Alpha) * ema);   
            }
        }

        public double Median {
            get
            {
                if (_quotes.Count == 0) return 0;
                var sortedArray = _quotes.ToArray();
                Array.Sort(sortedArray);

                return sortedArray[sortedArray.Length / 2 - 1 + sortedArray.Length % 2];
            }
        }
    }
}