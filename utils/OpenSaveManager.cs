using System;
using System.Collections.Generic;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;
using System.IO;

namespace WarThunderParser.Utils
{
    public class SaveEventArgs : EventArgs
    {
        public string FileName { get; private set; }

        public SaveEventArgs(string filename)
        {
            FileName = filename;
        }
    }
    public class SaveManager
    {
        public delegate void SaveEventHandler(SaveEventArgs eventArgs);
        public event SaveEventHandler OnSave;
        readonly string _defaultDirectory;
        private string LastFileName { get; set; }
        private Saver LastFileSaver { get; set; }

        public SaveManager(string defaultDirectory)
        {
            _defaultDirectory = defaultDirectory;
        }
        public SaveManager()
        {
            LastFileName = "";
            _defaultDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }

        public bool SaveLast(object toSave)
        {
            if (LastFileName == null) return false;
            Save(LastFileName,LastFileSaver,toSave);
            OnSave(new SaveEventArgs(LastFileName));
            return true;
        }
        public void Save(string fileName, Saver saver, object toSave)
        {
            saver.Save(fileName, toSave);
            OnSave(new SaveEventArgs(fileName));
        }
        public void Save(Dictionary<string, Saver> extensions, object toSave)
        {
            if (extensions.Count == 0)
            {
                throw new ArgumentException("Не задано ни одного расширения");
            }
            var dlg = new SaveFileDialog {InitialDirectory = _defaultDirectory, Filter = ""};
            var keys = new string[extensions.Count];
            extensions.Keys.CopyTo(keys, 0);
            foreach (string s in keys)
            {
                dlg.Filter += s;
            }
            var result = dlg.ShowDialog();
            if (result != true) return;
            extensions[keys[dlg.FilterIndex-1]].Save(dlg.FileName, toSave);
            if (OnSave != null)
            {
                OnSave(new SaveEventArgs(dlg.FileName));
            }
            LastFileName = dlg.FileName;
            LastFileSaver = extensions[keys[dlg.FilterIndex-1]];
        }
        public void Save(Dictionary<string, Saver> extensions, object toSave, string defName)
        {
            if (extensions.Count == 0)
            {
                throw new ArgumentException("Не задано ни одного расширения");
            }
            var dlg = new SaveFileDialog { InitialDirectory = _defaultDirectory, Filter = "", FileName = defName};
            var keys = new string[extensions.Count];
            extensions.Keys.CopyTo(keys, 0);
            foreach (string s in keys)
            {
                dlg.Filter += s;
            }
            var result = dlg.ShowDialog();
            if (result != true) return;
            extensions[keys[dlg.FilterIndex - 1]].Save(dlg.FileName, toSave);
            if (OnSave != null)
            {
                OnSave(new SaveEventArgs(dlg.FileName));
            }
            LastFileName = dlg.FileName;
            LastFileSaver = extensions[keys[dlg.FilterIndex - 1]];
        }
    }
    public abstract class Saver
    {
        protected Saver()
        {
        }

        public abstract void Save(string fileName, object toSave);
        public abstract object Open(string fileName);
    }
    public class BinSaver : Saver
    {
        public override void Save(string fileName, object toSave)
        {
            var type = toSave.GetType();
            using (var fstream = new System.IO.FileStream(fileName, System.IO.FileMode.OpenOrCreate))
            {
                var binFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binFormatter.Serialize(fstream, toSave);
            }
        }

        public override object Open(string fileName)
        {
            if (!File.Exists(fileName)) return null;
            using (var fstream = new System.IO.FileStream(fileName, System.IO.FileMode.Open))
            {
                var binFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();

                return binFormatter.Deserialize(fstream);
            }
        }
    }
    public class OpenManager
    {
        readonly string _defaultDirectory;
        public OpenManager(string defaultDirectory)
        {
            _defaultDirectory = defaultDirectory;
        }
        public OpenManager()
        {
            _defaultDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }
        public object Open(string fileName, Saver saver)
        {
            return saver.Open(fileName);
        }

        public object Open(Dictionary<string, Saver> extensions)
        {
            if (extensions.Count == 0)
            {
                throw new ArgumentException("Не задано ни одного расширения");
            }
            var dlg = new OpenFileDialog { InitialDirectory = _defaultDirectory, Filter = "" };
            var keys = new string[extensions.Count];
            extensions.Keys.CopyTo(keys, 0);
            foreach (string s in keys)
            {
                dlg.Filter += s;
            }
            var result = dlg.ShowDialog();
            if (result == true)
            {
                return extensions[keys[dlg.FilterIndex-1]].Open(dlg.FileName);
            }
            return null;
        }
        public Dictionary<string, object> OpenMultiple(Dictionary<string, Saver> extensions)
        {
            if (extensions.Count == 0)
            {
                throw new ArgumentException("Не задано ни одного расширения");
            }
            var dlg = new OpenFileDialog { InitialDirectory = _defaultDirectory, Filter = "", Multiselect = true};
            var keys = new string[extensions.Count];
            extensions.Keys.CopyTo(keys, 0);
            foreach (string s in keys)
            {
                dlg.Filter += s;
            }
            var result = dlg.ShowDialog();
            
            if (result == true)
            {
                Dictionary<string, object> resultDictionary = new Dictionary<string, object>();
                foreach (var fileName in dlg.FileNames)
                {
                    string fn = Path.GetFileNameWithoutExtension(fileName);
                    resultDictionary.Add(fn, extensions[keys[dlg.FilterIndex - 1]].Open(fileName));
                }
                return resultDictionary;
            }
            return null;
        }
    }

    public class AutoSaveManager
    {
        public string Extension { get; private set; }
        public string DefaultDirectory { get; private set; }
        public int TimerInterval { get; private set; } // Секунды
        public int ChangesToSaveCount { get; private set; }
        private bool _enabled;
        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                if (value)
                {
                    _timer.Change(TimerInterval*1000, TimerInterval*1000);
                    _changesCount = 0;
                }
                else
                {
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
        }
        private int _changesCount;
        public int CurChanges { get { return _changesCount; } }
        private readonly Timer _timer;
        private readonly Saver _saver;
        public object ObjectToSave { get; set; }
    
        public AutoSaveManager(string extension, string defDirectory, int interval, int changesCount, Saver saver, object objectToSave)
        {
            Extension = extension;
            DefaultDirectory = defDirectory;
            TimerInterval = interval;
            ChangesToSaveCount = changesCount;
            _timer = new Timer(AutoSave, null, TimerInterval*1000, TimerInterval*1000);
            _saver = saver;
            ObjectToSave = objectToSave;
            Enabled = true;
        }
        public void Change()
        {
            if (!Enabled) return;
            if (++_changesCount >= ChangesToSaveCount)
            {
                AutoSave(null);
            }
        }

        public void Reset()
        {
            _changesCount = 0;
            _timer.Change(TimerInterval * 1000, TimerInterval * 1000);
        }
        private void AutoSave(object autoSaveInfo)
        {
            string searchPattern = "autosave" + Extension;
            if(_changesCount==0) return;
            _changesCount = 0;
            _timer.Change(TimerInterval * 1000, TimerInterval * 1000);
            if (!Directory.Exists(DefaultDirectory))
            {
                Directory.CreateDirectory(DefaultDirectory);
            }
            var files = Directory.GetFiles(DefaultDirectory, searchPattern);
            Array.Sort(files);
            if (files.Length > 9)
            {
                for (int i = 9; i < files.Length; i++)
                {
                    File.Delete(files[i]);
                }
            }
            _saver.Save(DefaultDirectory + "autosave"+"_"+DateTime.Now.Month.ToString()+"_"+DateTime.Now.Day.ToString()+"_"+DateTime.Now.Hour.ToString()+"_"+DateTime.Now.Minute.ToString()+"_"+DateTime.Now.Second.ToString()+(Extension.Remove(0,1)),ObjectToSave);
        }
    }
}
