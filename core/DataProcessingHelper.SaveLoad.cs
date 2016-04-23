using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WarThunderParser.Core
{
    public partial class DataProcessingHelper
    {
        public SavedState getSavedState()
        {
            return new SavedState(this);
        }

        public void loadState(SavedState state)
        {
            Clear();
            m_Data = state.data;
            m_Units = state.units;
            Graphs = state.graphs;
            m_Abs = Graphs.Count > 0
                ? Graphs.First().XAxis
                : null;
            m_DataSize = (m_Data != null && m_Data.Count > 0) ? m_Data.First().Value.Count() : 0;

            Redraw();
            UpdateDataGrid();
        }

        [Serializable]
        public sealed class SavedState
        {
            [NonSerialized]
            private string name;

            internal Dictionary<string, List<double>> data;
            internal Dictionary<string, string> units;
            internal DateTime? synchTyme;
            internal List<Graph> graphs;
            
            internal SavedState(DataProcessingHelper parent)
            {
                data = parent.m_Data;
                units = parent.m_Units;
                graphs = parent.Graphs;
                name = string.IsNullOrEmpty(parent.m_Type)
                    ? ((graphs != null && graphs.Count > 0) ? graphs.First().GraphName : "")
                    : parent.m_Type;
            }

            internal string getName()
            {
                return name;
            }

        }
    }
}
