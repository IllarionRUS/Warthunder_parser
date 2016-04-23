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
        //Определяет критический ли рекордер. Сбой критического рекордера вызывает сбой всего сбора данных.
        public bool IsCritical { get; private set; }
        //Максимальное время ожидания до генерации ошибки
        public int Delay { get; private set; }
        // Формат дробного числа для парсинга
        private readonly NumberFormatInfo _numFormatInfo = new CultureInfo("en-US", false).NumberFormat;
        //Словарь, хранящий собранные данные
        private Dictionary<string, ParsedParam> _paramsDictionary = new Dictionary<string, ParsedParam>();
        //Возвращает массив ключей словаря - имен собранных параметров.
        public string[] Names{get { return _paramsDictionary.Keys.ToArray(); }}
        //Возвращает массив значений параметра по заданному имени
        public List<double> Values(string name)
        {
            return _paramsDictionary[name].Value;
        }
        //Возвращает единицу измерения параметра
        public string Unit(string name)
        {
            return _paramsDictionary[name].Unit;
        }
        //Возвращает количество параметров в словаре
        public int ParametersCount
        {
            get { return _paramsDictionary.Count; }
        }
        //Возвращает количество значений каждого параметра
        public int ValuesCount
        {
            get { return _paramsDictionary[Consts.Value.Time].Value.Count; }
        }
        //Время начала сбора данных
        public DateTime InitTime { get; private set; }
        //Ссылка на источник данных
        public string Uri { get; private set; }
        //Возвращает true, если данные были инициализированы
        public bool HaveSomeData { get { return (_paramsDictionary != null)&&(ParametersCount>0)&&(ValuesCount>0); } }
        public delegate void FailEventHandler(FdrRecorderFailureEventArgs e);
        //Событие, возникающее при сбое рекордера
        public event FailEventHandler OnFailure;
        public FdrRecorderFailureEventArgs _failureArgs;
        private DispatcherTimer _failureTimer;
        
        // One-time text params snapshot
        public Dictionary<string, string> TextData { get; private set; }

        //Конструктор
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
        //Запрос к ресурсу и возврат результата в формате строки
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
        //Парсинг данных полученного ответа.
        void ParseResponse(string content)
        {
            _failureTimer.Stop();
            //Совпадения, включающие в себя все строки данных
            var matches = Regex.Matches(content, "\".+\":.+(,|})");
            //Текущая временная метка
            var mark = DateTime.Now;
            //Словарь текущих значений
            var curValues = new Dictionary<string, string[]>();
             // Имя параметра времени
            //При инициализации
            if (_paramsDictionary.Count==0)
            {
                _paramsDictionary.Add(Consts.Value.Time, new ParsedParam {Value = new List<double>(), Unit = Consts.Unit.Time_Ms});
                InitTime = mark;
            }
            //Преобразование временной метки в интервал от начала записи и добавление значения в словарь текущих значений.
            //Несмотря на то, что удобнее добавить значение времени в общий словарь напрямую, добавление в текущий словарь реализовано для единообразия 
            //и более удобного сравнения ключей в дальнейшем.
            curValues.Add(Consts.Value.Time, new[] { Consts.Unit.Time_Ms, (mark - InitTime).TotalMilliseconds.ToString(_numFormatInfo) });
           
           
            //Сбор текущих значений всех параметров, кроме времени
            foreach (Match match in matches)
            {
                //Строку "valid": true пропускаем
                if (match.Value.Contains("\"valid\"")) continue;
                string name, unit, textValue;
                //Отправляем значение текущего совпадения в другую переменную для дальнейших манипуляций
                var matchValue = match.Value;
                //Находим совпадение по паттерну значения текущей строки данных
                var curMatch = Regex.Match(matchValue, ":\\s+[-0-9.]+(,|})");
                //Удаляем строку содержащее значение параметра из общей строки совпадения, в строке остались значение ед. измерения и имя параметра
                matchValue = matchValue.Remove(curMatch.Index);
                //Запоминаем текущее значение параметра из совпадения, оставляя только цифры и точку.
                textValue = Regex.Match(curMatch.Value, "[-0-9.]+").Value;
                //Если оставшаяся строка не содержит запятую, то значения ед. измерения у нее нет
                if (!matchValue.Contains(","))
                {
                    unit = "";
                }
                else
                {
                    //Иначе находим совпадение по паттерну ед.измерения текущей строки данных
                    curMatch = Regex.Match(matchValue, ",.+\"");
                    // Удаляем из оставшейся строки данных строку, содержащую единицу измерения
                    matchValue = matchValue.Remove(curMatch.Index);
                    //Запоминаем текущую ед.измерения параметра из совпадения, очищая ее от лишних символов.
                    unit = curMatch.Value.Remove(curMatch.Value.Length - 1).Remove(0, 2);
                }
                //Оставшаяся строка содержит только имя и передние кавычки, убираем кавычки и запоминаем имя.
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
                    //Добавляем в словарь текущих значений параметр
                    curValues.Add(name, new[] { unit, textValue });
                    //При инициализации
                    if (_paramsDictionary[Consts.Value.Time].Value.Count == 0)
                    {
                        var parsedParam = new ParsedParam { Value = new List<double>(), Unit = unit };
                        _paramsDictionary.Add(name, parsedParam);
                    }
                }                
            }
            //Проверяем есть ли не совпадение в ключах общего и текущего словарей
            if (_paramsDictionary.Keys.SequenceEqual(curValues.Keys.ToArray()))
            {
                //Если есть - проверяем, есть ли в общем словаре ключи, данные на которые не представлены в текущем ответе, если таковые есть
                //добавляем в параметр общего словаря с таким ключом значение 0
                var emptyKeys = _paramsDictionary.Keys.Except(curValues.Keys).ToArray();
                foreach (var emptyKey in emptyKeys)
                {
                    _paramsDictionary[emptyKey].Value.Add(0);
                }
                //Проверяем есть ли в собранных данных ключи, которых нет в общем словаре, если таковые есть
                //добавляем в общий словарь параметр с таким ключом и инициализируем все значения до текущего момента времени нулями.
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
            //Вносим в общий словарь текущие значения параметров
            //Array.ForEach(curValues.Where(keyValue => string.IsNullOrEmpty(keyValue.Key.Trim())).ToArray(), 
            //   keyValue => curValues.Remove(keyValue.Key));
            foreach (var valuePair in curValues)
            {
                _paramsDictionary[valuePair.Key].Value.Add(double.Parse(valuePair.Value[1], _numFormatInfo));
                
            }
        }
        //Представление данных в виде массива значений с равными временными промежутками, интерполированными по заданному интервалу.
        //Время синхронизации требуется для единого нуля всех рекордеров.
        public List<double>[] GetApproxList(DateTime syncTime, int interpInterval)
        {
            //Интервал между временем инициализации и временем синхронизации
            var syncTimeSpan = InitTime - syncTime;
            //Получение номера начальной позициии и приравнивание его к нулю, если точка синхронизации находится позже точки инициализации
            int startPosition = (int) Math.Truncate(syncTimeSpan.TotalMilliseconds/interpInterval);
            if (startPosition < 0) startPosition = 0;
            //Массив списков в которые будут добавляться полученные значения
            //Каждый список соответствует параметру
            var result = new List<double>[_paramsDictionary.Count];
            //Получение массива значений из словаря
            var values = _paramsDictionary.Values.ToArray();
            //Позиция предыдущего цикла
            double prevSpan = 0;
            //Проходим по всем параметрам
            for (int i = 0; i < ParametersCount; i++)
            {
                //Инициализации списка значений результата
                result[i] = new List<double>();
                //Инициализации текущей позиции результата
                var currentPosition = startPosition*interpInterval;
                //Все значения до начального номера инициализируются нулем
                for (int j = 0; j < startPosition; j++)
                {
                    result[i].Add(0);
                }
                //Прсоход по всем значениям исходных данных параметра
                for (int j = 0; j < ValuesCount; j++)
                {
                    //Получение смещения во времени с учетом синхронизации
                    var realSpan = values[0].Value[j] + syncTimeSpan.TotalMilliseconds;
                    //Если текущая вреенная позиция находится до позиции синхронизации - переходим на следующую итерацию
                    if (realSpan < 0) continue;
                    //Добавляем новые значения в список значений результата, пока текущая позиция результата не превышает
                    //текущей позиции исходных данных
                    while (currentPosition < realSpan)
                    {
                        //Если интерполируется значение времени, то просто добавляем в значения результата текущую позицию
                        if (i == 0)
                        {
                            result[i].Add(currentPosition);
                        }
                        else
                        {
                            //Иначе получаем смещене текущей позиции исходных данных от предыдущей значением исходных данных.
                            var diffX_X0 = currentPosition - prevSpan;
                            //Получаем смещение текущей позиции результата от предыдущей позиции исходных данных
                            var diffX1_X0 = realSpan - prevSpan;
                            //Получаем разность значений исходных данных.
                            var funcdiffX1_X0 = values[i].Value[j] - values[i].Value[j - 1];
                            //Интерполируем
                            var mean = values[i].Value[j - 1] + (funcdiffX1_X0/diffX1_X0)*diffX_X0;
                            //Добавляем значение в список результатов.
                            result[i].Add(mean);
                        }
                        //Прибавляем интервал интерполяции к текущей позиции
                        currentPosition += interpInterval;
                    }
                    //Запоминаем значение текущей позиции исходных данных как предыдущей.
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