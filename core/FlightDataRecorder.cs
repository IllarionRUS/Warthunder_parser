using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Threading;
using WarThunderParser.Core;

namespace WarThunderParser
{
    public struct ParsedParam
    {
        public string Unit { get; set; }
        public List<double> Value;
    }

    public class FlightDataRecorder
    {
        //���������� ����������� �� ��������. ���� ������������ ��������� �������� ���� ����� ����� ������.
        public bool IsCritical { get; private set; }
        //������������ ����� �������� �� ��������� ������
        public int Delay { get; private set; }
        // ������ �������� ����� ��� ��������
        private readonly NumberFormatInfo _numFormatInfo = new CultureInfo("en-US", false).NumberFormat;
        //�������, �������� ��������� ������
        private Dictionary<string, ParsedParam> _paramsDictionary = new Dictionary<string, ParsedParam>();
        //���������� ������ ������ ������� - ���� ��������� ����������.
        public string[] Names{get { return _paramsDictionary.Keys.ToArray(); }}
        //���������� ������ �������� ��������� �� ��������� �����
        public List<double> Values(string name)
        {
            return _paramsDictionary[name].Value;
        }
        //���������� ������� ��������� ���������
        public string Unit(string name)
        {
            return _paramsDictionary[name].Unit;
        }
        //���������� ���������� ���������� � �������
        public int ParametersCount
        {
            get { return _paramsDictionary.Count; }
        }
        //���������� ���������� �������� ������� ���������
        public int ValuesCount
        {
            get { return _paramsDictionary[Consts.Value.Time].Value.Count; }
        }
        //����� ������ ����� ������
        public DateTime InitTime { get; private set; }
        //������ �� �������� ������
        public string Uri { get; private set; }
        //���������� true, ���� ������ ���� ����������������
        public bool HaveSomeData { get { return (_paramsDictionary != null)&&(ParametersCount>0)&&(ValuesCount>0); } }
        public delegate void FailEventHandler(FdrRecorderFailureEventArgs e);
        //�������, ����������� ��� ���� ���������
        public event FailEventHandler OnFailure;
        public FdrRecorderFailureEventArgs _failureArgs;
        private DispatcherTimer _failureTimer;
        
        // One-time text params snapshot
        public Dictionary<string, string> TextData { get; private set; }

        //�����������
        public FlightDataRecorder(string uri, bool isCritical, int delay)
        {
            EventHandler failureTimerAction = delegate
            {
                if (OnFailure != null)
                    OnFailure(_failureArgs);
                _failureTimer.Stop();
            };
            Delay = delay;
            Uri = uri;
            IsCritical = isCritical;
            _failureTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(Delay), DispatcherPriority.Normal, failureTimerAction,
                Dispatcher.CurrentDispatcher);
            _failureTimer.IsEnabled = false;

            TextData = new Dictionary<string, string>();
        }
        public void Request()
        {
            var response = GetResponse();
            if(response==null)return;
            ParseResponse(response);
        }
        //������ � ������� � ������� ���������� � ������� ������
        string GetResponse()
        {
            StreamReader resultStreamReader = null;
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(Uri);
                var response = (HttpWebResponse)request.GetResponse();
                resultStreamReader = new StreamReader(response.GetResponseStream());
            }
            catch (WebException e)
            {
                _failureArgs = new FdrRecorderFailureEventArgs(this, e.Message);
                _failureTimer.Start();
                return null;
            }
            var result = resultStreamReader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(result))
            {
                _failureArgs = new FdrRecorderFailureEventArgs(this, "Response is empty");
                _failureTimer.Start();
                return null;
            }
            if (result.Contains("\"valid\": false"))
            {
                _failureArgs = new FdrRecorderFailureEventArgs(this, "Not valid");
                _failureTimer.Start();
                return null;
            }
            return result;
        }
        //������� ������ ����������� ������.
        void ParseResponse(string content)
        {
            _failureTimer.Stop();
            //����������, ���������� � ���� ��� ������ ������
            var matches = Regex.Matches(content, "\".+\":.+(,|})");
            //������� ��������� �����
            var mark = DateTime.Now;
            //������� ������� ��������
            var curValues = new Dictionary<string, string[]>();
             // ��� ��������� �������
            //��� �������������
            if (_paramsDictionary.Count==0)
            {
                _paramsDictionary.Add(Consts.Value.Time, new ParsedParam {Value = new List<double>(), Unit = Consts.Unit.Time_Ms});
                InitTime = mark;
            }
            //�������������� ��������� ����� � �������� �� ������ ������ � ���������� �������� � ������� ������� ��������.
            //�������� �� ��, ��� ������� �������� �������� ������� � ����� ������� ��������, ���������� � ������� ������� ����������� ��� ������������ 
            //� ����� �������� ��������� ������ � ����������.
            curValues.Add(Consts.Value.Time, new[] { Consts.Unit.Time_Ms, (mark - InitTime).TotalMilliseconds.ToString(_numFormatInfo) });
           
           
            //���� ������� �������� ���� ����������, ����� �������
            foreach (Match match in matches)
            {
                //������ "valid": true ����������
                if (match.Value.Contains("\"valid\"")) continue;
                string name, unit, textValue;
                //���������� �������� �������� ���������� � ������ ���������� ��� ���������� �����������
                var matchValue = match.Value;
                //������� ���������� �� �������� �������� ������� ������ ������
                var curMatch = Regex.Match(matchValue, ":\\s+[-0-9.]+(,|})");
                //������� ������ ���������� �������� ��������� �� ����� ������ ����������, � ������ �������� �������� ��. ��������� � ��� ���������
                matchValue = matchValue.Remove(curMatch.Index);
                //���������� ������� �������� ��������� �� ����������, �������� ������ ����� � �����.
                textValue = Regex.Match(curMatch.Value, "[-0-9.]+").Value;
                //���� ���������� ������ �� �������� �������, �� �������� ��. ��������� � ��� ���
                if (!matchValue.Contains(","))
                {
                    unit = "";
                }
                else
                {
                    //����� ������� ���������� �� �������� ��.��������� ������� ������ ������
                    curMatch = Regex.Match(matchValue, ",.+\"");
                    // ������� �� ���������� ������ ������ ������, ���������� ������� ���������
                    matchValue = matchValue.Remove(curMatch.Index);
                    //���������� ������� ��.��������� ��������� �� ����������, ������ �� �� ������ ��������.
                    unit = curMatch.Value.Remove(curMatch.Value.Length - 1).Remove(0, 2);
                }
                //���������� ������ �������� ������ ��� � �������� �������, ������� ������� � ���������� ���.
                name = matchValue.Replace("\"", "");
                //ignore empty names (no matches with regex)
                if (string.IsNullOrEmpty(name.Trim()))
                {
                    // add text data
                    string dividedBySpace = Regex.Replace(match.Value, "[\",:]", "");
                    var split = dividedBySpace.Split(" ".ToCharArray());
                    if (split.Length == 2 && !TextData.ContainsKey(split[0]))
                        TextData.Add(split[0], split[1]);
                }
                else
                {
                    // add numeric data
                    //��������� � ������� ������� �������� ��������
                    curValues.Add(name, new[] { unit, textValue });
                    //��� �������������
                    if (_paramsDictionary[Consts.Value.Time].Value.Count == 0)
                    {
                        var parsedParam = new ParsedParam { Value = new List<double>(), Unit = unit };
                        _paramsDictionary.Add(name, parsedParam);
                    }
                }                
            }
            //��������� ���� �� �� ���������� � ������ ������ � �������� ��������
            if (_paramsDictionary.Keys.SequenceEqual(curValues.Keys.ToArray()))
            {
                //���� ���� - ���������, ���� �� � ����� ������� �����, ������ �� ������� �� ������������ � ������� ������, ���� ������� ����
                //��������� � �������� ������ ������� � ����� ������ �������� 0
                var emptyKeys = _paramsDictionary.Keys.Except(curValues.Keys).ToArray();
                foreach (var emptyKey in emptyKeys)
                {
                    _paramsDictionary[emptyKey].Value.Add(0);
                }
                //��������� ���� �� � ��������� ������ �����, ������� ��� � ����� �������, ���� ������� ����
                //��������� � ����� ������� �������� � ����� ������ � �������������� ��� �������� �� �������� ������� ������� ������.
                var missingKeys = curValues.Keys.Except(_paramsDictionary.Keys).ToArray();
                foreach (var missingKey in missingKeys)
                {
                    _paramsDictionary.Add(missingKey,
                        new ParsedParam {Unit = curValues[missingKey][0], Value = new List<double>()});
                    for (int i = 0; i < _paramsDictionary[Consts.Value.Time].Value.Count - 1; i++)
                    {
                        _paramsDictionary[missingKey].Value.Add(0);
                    }
                }
            }
            //������ � ����� ������� ������� �������� ����������
            //Array.ForEach(curValues.Where(keyValue => string.IsNullOrEmpty(keyValue.Key.Trim())).ToArray(), 
            //   keyValue => curValues.Remove(keyValue.Key));
            foreach (var valuePair in curValues)
            {
                _paramsDictionary[valuePair.Key].Value.Add(double.Parse(valuePair.Value[1], _numFormatInfo));
                
            }
        }
        //������������� ������ � ���� ������� �������� � ������� ���������� ������������, ������������������ �� ��������� ���������.
        //����� ������������� ��������� ��� ������� ���� ���� ����������.
        public List<double>[] GetApproxList(DateTime syncTime, int interpInterval)
        {
            //�������� ����� �������� ������������� � �������� �������������
            var syncTimeSpan = InitTime - syncTime;
            //��������� ������ ��������� �������� � ������������� ��� � ����, ���� ����� ������������� ��������� ����� ����� �������������
            int startPosition = (int) Math.Truncate(syncTimeSpan.TotalMilliseconds/interpInterval);
            if (startPosition < 0) startPosition = 0;
            //������ ������� � ������� ����� ����������� ���������� ��������
            //������ ������ ������������� ���������
            var result = new List<double>[_paramsDictionary.Count];
            //��������� ������� �������� �� �������
            var values = _paramsDictionary.Values.ToArray();
            //������� ����������� �����
            double prevSpan = 0;
            //�������� �� ���� ����������
            for (int i = 0; i < ParametersCount; i++)
            {
                //������������� ������ �������� ����������
                result[i] = new List<double>();
                //������������� ������� ������� ����������
                var currentPosition = startPosition*interpInterval;
                //��� �������� �� ���������� ������ ���������������� �����
                for (int j = 0; j < startPosition; j++)
                {
                    result[i].Add(0);
                }
                //������� �� ���� ��������� �������� ������ ���������
                for (int j = 0; j < ValuesCount; j++)
                {
                    //��������� �������� �� ������� � ������ �������������
                    var realSpan = values[0].Value[j] + syncTimeSpan.TotalMilliseconds;
                    //���� ������� �������� ������� ��������� �� ������� ������������� - ��������� �� ��������� ��������
                    if (realSpan < 0) continue;
                    //��������� ����� �������� � ������ �������� ����������, ���� ������� ������� ���������� �� ���������
                    //������� ������� �������� ������
                    while (currentPosition < realSpan)
                    {
                        //���� ��������������� �������� �������, �� ������ ��������� � �������� ���������� ������� �������
                        if (i == 0)
                        {
                            result[i].Add(currentPosition);
                        }
                        else
                        {
                            //����� �������� ������� ������� ������� �������� ������ �� ���������� ��������� �������� ������.
                            var diffX_X0 = currentPosition - prevSpan;
                            //�������� �������� ������� ������� ���������� �� ���������� ������� �������� ������
                            var diffX1_X0 = realSpan - prevSpan;
                            //�������� �������� �������� �������� ������.
                            var funcdiffX1_X0 = values[i].Value[j] - values[i].Value[j - 1];
                            //�������������
                            var mean = values[i].Value[j - 1] + (funcdiffX1_X0/diffX1_X0)*diffX_X0;
                            //��������� �������� � ������ �����������.
                            result[i].Add(mean);
                        }
                        //���������� �������� ������������ � ������� �������
                        currentPosition += interpInterval;
                    }
                    //���������� �������� ������� ������� �������� ������ ��� ����������.
                    prevSpan = realSpan;
                }
            }
            return result;
        }

        public void Clear()
        {
            _paramsDictionary.Clear();
            TextData.Clear();
        }
    }
}