using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None, AddIndicators = true)]
    public class draftstoch : Robot
    {

        // Parameters //
        [Parameter("WMA Period", MinValue = 1)]
        public int WmaPeriod { get; set; }
        
        // Indicators //
        private WeightedMovingAverage _wma;
        private StochasticOscillator _oscillator;
        
        // Classes //
        public class CriticalPoint
        {
            public DateTime TimePoint { get; set; }
            public double Value { get; set; }
            public int Type { get; set; }

            public CriticalPoint(int type, double value, Robot robot)
            {
                this.Type = type;
                this.Value = value;
                this.TimePoint = robot.Bars.OpenTimes.Last(3);
            }
        }
        
        // Variables //
        public double wmaSlope;
        public double oscillatorSlope;
        
        CriticalPoint[] StochasticCP = new CriticalPoint[4];
        CriticalPoint[] PriceCP = new CriticalPoint[4];
        
        // Custom Functions //
        public CriticalPoint[] AppendOnRotation(CriticalPoint data, CriticalPoint[] original)
        {
            CriticalPoint[] FinalResult = new CriticalPoint[original.Length];
            for (int i = 0; i < original.Length - 1; i++)
            {
                FinalResult[i] = original[i + 1];
            }

            FinalResult[original.Length - 1] = data;
            
            return FinalResult;
        }
        
        public bool isCP(ref double givenSlope, double previousValue, double ancestorValue, ref CriticalPoint[] targetArray, string indicator)
        {
            if ((previousValue - ancestorValue) * givenSlope < 0)
            {
                int typeOfPoint = 0;
                
                if (givenSlope < 0)
                {
                    typeOfPoint = 1; // min
                }
                else
                {
                    typeOfPoint = 2; // max
                }

                CriticalPoint newCP;
                if (indicator == "oscillator")
                {
                     newCP = new CriticalPoint(typeOfPoint, _oscillator.PercentK.Last(2), this);
                }
                else if (indicator == "wma")
                {
                    newCP = new CriticalPoint(typeOfPoint, _wma.Result.Last(2), this);
                }
                else
                {
                    newCP = null;
                }
                
                targetArray = AppendOnRotation(newCP, targetArray);

                givenSlope = previousValue - ancestorValue;
                return true;
            }

            givenSlope = previousValue - ancestorValue;
            return false;
        }

        protected override void OnStart()
        {
            _wma = Indicators.WeightedMovingAverage(Bars.ClosePrices, WmaPeriod);
            _oscillator = Indicators.StochasticOscillator(9, 1, 3, MovingAverageType.Simple);
        }

        protected override void OnBar()
        {
            isCP(ref wmaSlope, _wma.Result.LastValue, _wma.Result.Last(2), ref PriceCP, "wma");
            isCP(ref oscillatorSlope, _oscillator.PercentK.LastValue, _oscillator.PercentK.Last(2), ref StochasticCP, "oscillator");
            
            Print("StochasticCP:");
            foreach (var cp in StochasticCP)
            {
                if (cp != null) // Avoid null references
                    Print($"Time: {cp.TimePoint}, Type: {cp.Type}, Value: {cp.Value}");
                else
                    Print("Null entry in StochasticCP");
            }

            // Print PriceCP array
            Print("PriceCP:");
            foreach (var cp in PriceCP)
            {
                if (cp != null) // Avoid null references
                    Print($"Time: {cp.TimePoint}, Type: {cp.Type}, Value: {cp.Value}");
                else
                    Print("Null entry in PriceCP");
            }
        }

        protected override void OnStop()
        {

        }
    }
}
