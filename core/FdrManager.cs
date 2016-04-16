using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Navigation;

namespace WarThunderParser
{
    //Перечесление состояний менеджера - не запущен, в процессе сбора, сбор завершен, произошел сбой.
    public enum ManagerState {NotStarted, Collecting, DataCollected, Failure};
    //Класс управления рекордерами, запускает сбор данных заданного списка рекордеров, останавливает его и
    //обрабатывает события, генерируемые рекордерами.
    public class FdrManager
    {
         //Рекордеры поступившие при инициализации.
        public List<FlightDataRecorder> Recorders { get; set; }
        //Рекордеры, закончившие сбор без ошибок
        private List<FlightDataRecorder> _finishedRecorders; 
        //Потоки рекордеров, нужны для асинхронных запросов, наверняка можно это сделать без потоков, но я еще не разобрался как.
        //Возможно стоит перенести логику повторения запросов в сам рекордер, а здесь только давать команду на пуск,стоп и инициализировать интервалом?
        private Dictionary<FlightDataRecorder, Thread> _recorderThreads;
        //Состояние сбора данных
        public ManagerState State { get; private set; }
        //Интервал запросов(точнее пауза между ними)
        public int RequestInterval { get;private set; }

        public delegate void FdrEventHandler(FdrManagerEventArgs e);
        public delegate void FdrRecorderEventHandler(FdrRecorderFailureEventArgs e);
        //Событие при начале сбора
        public event FdrEventHandler OnStartDataCollecting;
        //Событие при успешном завершение сбора
        public event FdrEventHandler OnDataCollected;
        //Событие при сбое критического рекордера.
        public event FdrEventHandler OnTotalFailure;
        //Событие при сбое некритического рекордера.
        public event FdrRecorderEventHandler OnRecorderFailure;
        //Конструктор
        public FdrManager(FlightDataRecorder[] recorders, int requestinterval)
        {
            Recorders = new List<FlightDataRecorder>(recorders);
            _finishedRecorders = new List<FlightDataRecorder>(recorders);
            RequestInterval = requestinterval;
            _recorderThreads = new Dictionary<FlightDataRecorder,Thread>(); 
        }
        //Обработчик сбоя рекордера, обрабатывает сбой и генерирует
        //соответствующее типу сбоя событие.
        void RecorderFailure(FdrRecorderFailureEventArgs e)
        {
            if (e.Recorder.IsCritical)
            {
                if (OnTotalFailure!=null) OnTotalFailure(new FdrManagerEventArgs("Сбой критического сборщика данных"));
                State = ManagerState.Failure;
                _finishedRecorders.Clear();
            }
            else
            {
                if (OnRecorderFailure != null) OnRecorderFailure(e);
                _finishedRecorders.Remove(e.Recorder);
                _recorderThreads[e.Recorder].Abort();
            }
        }
        //Дейтсвие, которым инициализируются потоки, посылает сообщение рекордеру на запрос параметров
        //и выжидает указанную паузу.
        private void RequestDataAction(object recorder)
        {
            var flightDataRecorder = (FlightDataRecorder) recorder;
            while (State == ManagerState.Collecting)
            {
                flightDataRecorder.Request();
                Thread.Sleep(RequestInterval);
            }
        }
        //Метод начала сбора данных, инициализирует список потоков соответствующими рекордерами, запускает потоки и
        //генерирует соответствующее событие.
        public void StartDataCollect()
        {
            _recorderThreads.Clear();
            State = ManagerState.Collecting;
            foreach (var flightDataRecorder in Recorders) 
            {
                flightDataRecorder.Clear();
                flightDataRecorder.OnFailure += RecorderFailure;
                _recorderThreads.Add(flightDataRecorder, new Thread(RequestDataAction));
                _recorderThreads[flightDataRecorder].Start(flightDataRecorder);
            }
            if(OnStartDataCollecting!=null) OnStartDataCollecting(new FdrManagerEventArgs(""));
        }
        //Метод завершения сбора данных, устанавливает состояние сбора и генерирует соответствующее событие.
        public void StopDataCollect()
        {
            if((State!=ManagerState.Collecting)||(State == ManagerState.DataCollected)) return;
            foreach (
                var flightDataRecorder in
                    (new List<FlightDataRecorder>(_finishedRecorders)).Where(
                        flightDataRecorder => (!flightDataRecorder.HaveSomeData)))

                if (flightDataRecorder.IsCritical)
                {
                    OnTotalFailure(new FdrManagerEventArgs("Не удалось собрать данные."));
                    return;
                }
                else
                    _finishedRecorders.Remove(flightDataRecorder);
                State = ManagerState.DataCollected;
                if (OnDataCollected != null)
                    OnDataCollected(new FdrManagerEventArgs(""));
        }
        // Инициализирует адаптер данными с рекордеров 
        public bool InitializeAdapter(FdrDataAdapter adapter)
        {
            if (State != ManagerState.DataCollected) return false;
            adapter.Initialize(_finishedRecorders.ToArray());
            return true;
        }

        public List<FlightDataRecorder> getFinishedRecorders()
        {
            return State == ManagerState.DataCollected
                ? _finishedRecorders
                : null;
        }

    }

    public class FdrManagerEventArgs : EventArgs
    {
        public string Message { get; private set; }

        public FdrManagerEventArgs(string message)
        {
            Message = message;
        }
    }
    public class FdrRecorderFailureEventArgs : EventArgs
    {
        public FlightDataRecorder Recorder;
        public string Reason { get; private set; }
        public FdrRecorderFailureEventArgs(FlightDataRecorder recorder, string reason)
        {
            Recorder = recorder;
            Reason = reason;
        }
    }
}
